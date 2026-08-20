from functools import lru_cache
from itertools import combinations
from pathlib import Path
import os
import re
import subprocess

import cv2
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parent.parent
DETECTOR_PATH = Path(os.getenv("PLATELENS_DETECTOR", ROOT / "models/plate.pt"))
CLASSIFIER_PATH = Path(os.getenv("PLATELENS_CLASSIFIER", ROOT / "models/plate-type.pt"))
OCR_PATH = Path(os.getenv("PLATELENS_OCR", ROOT / "vision/apple-ocr"))
MAX_IMAGE_BYTES = 10 * 1024 * 1024
PLATE_PATTERN = re.compile(r"^[A-Z]{3}(?:[0-9]{4}|[0-9][A-Z][0-9]{2})$")
ALLOWED_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

app = FastAPI(title="PlateLens Vision", docs_url=None, redoc_url=None)


def normalize_text(value: str) -> str:
    """Converte o OCR para maiúsculas e remove caracteres impossíveis em placas."""
    return "".join(character for character in value.upper() if character in ALLOWED_CHARS)


def fit_plate(value: str, plate_type: str) -> tuple[str, int] | None:
    """Ajusta confusões comuns entre letras e números conforme o padrão da placa."""
    letters = {"0": "O", "1": "I", "2": "Z", "5": "S", "6": "G", "7": "T", "8": "B"}
    digits = {value: key for key, value in letters.items()}
    expected = "LLLDLDD" if plate_type == "MERCOSUL" else "LLLDDDD"
    for window in (value[index:index + 7] for index in range(max(1, len(value) - 6))):
        if len(window) != 7:
            continue
        corrected, changes = [], 0
        for character, kind in zip(window, expected):
            replacement = letters.get(character) if kind == "L" and character.isdigit() else digits.get(character) if kind == "D" and character.isalpha() else character
            if replacement is None or (kind == "L") != replacement.isalpha():
                break
            corrected.append(replacement)
            changes += replacement != character
        else:
            plate = "".join(corrected)
            if PLATE_PATTERN.fullmatch(plate):
                return plate, changes
    return None


def choose_ocr_text(results: list, plate_type: str, type_confidence: float = 1.0) -> tuple[str, float, str]:
    """Combina candidatos do OCR e escolhe a placa válida com melhor pontuação."""
    parts = [(normalize_text(text), float(confidence)) for _, text, confidence in results[:8]]
    parts = [part for part in parts if part[0]]
    valid = []
    for count in range(1, len(parts) + 1):
        for selected in combinations(parts, count):
            for candidate_type in ("MERCOSUL", "ANTIGA"):
                fitted = fit_plate("".join(text for text, _ in selected), candidate_type)
                if fitted:
                    plate, changes = fitted
                    preference = .01 * type_confidence if candidate_type == plate_type else 0
                    valid.append((plate, min(score for _, score in selected) - changes * .03 - (count - 1) * .01 + preference, candidate_type))
    return max(valid or [(text, score, plate_type) for text, score in parts] or [("", 0.0, plate_type)], key=lambda candidate: candidate[1])


def rectify_plate(crop: np.ndarray) -> np.ndarray:
    """Corrige a perspectiva do recorte quando encontra os quatro cantos da placa."""
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(cv2.GaussianBlur(gray, (5, 5), 0), 40, 140)
    area = crop.shape[0] * crop.shape[1]
    for contour in sorted(cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)[0], key=cv2.contourArea, reverse=True)[:12]:
        polygon = cv2.approxPolyDP(contour, .025 * cv2.arcLength(contour, True), True)
        if len(polygon) != 4 or cv2.contourArea(polygon) < area * .18:
            continue
        points = polygon.reshape(4, 2).astype(np.float32)
        ordered = np.array([points[np.argmin(points.sum(1))], points[np.argmin(np.diff(points, axis=1))], points[np.argmax(points.sum(1))], points[np.argmax(np.diff(points, axis=1))]])
        top_left, top_right, bottom_right, bottom_left = ordered
        width = int(max(np.linalg.norm(bottom_right - bottom_left), np.linalg.norm(top_right - top_left)))
        height = int(max(np.linalg.norm(top_right - bottom_right), np.linalg.norm(top_left - bottom_left)))
        if height <= 0 or not 2 <= width / height <= 6:
            continue
        target = np.array([[0, 0], [width - 1, 0], [width - 1, height - 1], [0, height - 1]], dtype=np.float32)
        return cv2.warpPerspective(crop, cv2.getPerspectiveTransform(ordered, target), (width, height))
    return crop


def crop_quality(crop: np.ndarray) -> float:
    """Estima nitidez, exposição, tamanho e proporção do recorte entre zero e um."""
    height, width = crop.shape[:2]
    gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
    sharpness = min(1.0, cv2.Laplacian(gray, cv2.CV_64F).var() / 350)
    exposure = 1 - float(np.mean((gray < 20) | (gray > 245)))
    size = min(1.0, width / 220) * min(1.0, height / 55)
    aspect = max(0.0, 1 - abs(width / max(1, height) - 4) / 4)
    return round(.4 * sharpness + .25 * exposure + .25 * size + .1 * aspect, 4)


def prepare_ocr_image(band: np.ndarray) -> np.ndarray:
    """Amplia e realça a faixa de caracteres antes de entregá-la ao OCR."""
    scale = max(1, 900 / max(1, band.shape[1]))
    original = cv2.resize(band, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
    gray = cv2.cvtColor(original, cv2.COLOR_BGR2GRAY)
    enhanced = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply(gray)
    enhanced = cv2.addWeighted(enhanced, 1.5, cv2.GaussianBlur(enhanced, (0, 0), 1), -.5, 0)
    enhanced = cv2.cvtColor(enhanced, cv2.COLOR_GRAY2BGR)
    separator = np.zeros((12, original.shape[1], 3), dtype=np.uint8)
    return np.vstack((original, separator, enhanced))


def read_plate(crop: np.ndarray, plate_type: str, type_confidence: float) -> tuple[str, float, str]:
    """Executa o OCR nativo e transforma sua saída no melhor candidato de placa."""
    if not OCR_PATH.is_file():
        raise FileNotFoundError("Compile o OCR nativo com: make vision/apple-ocr")
    height, width = crop.shape[:2]
    top = int(height * (.2 if plate_type == "MERCOSUL" else .06))
    band = crop[top:int(height * .94), int(width * .09):int(width * .98)]
    encoded, content = cv2.imencode(".png", prepare_ocr_image(band))
    if not encoded:
        return "", 0.0, plate_type
    try:
        output = subprocess.run([str(OCR_PATH)], input=content.tobytes(), capture_output=True, timeout=3, check=True).stdout.decode()
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
        return "", 0.0, plate_type
    results = []
    for line in output.splitlines():
        confidence, separator, text = line.partition("\t")
        if separator:
            results.append((None, text, float(confidence)))
    return choose_ocr_text(results, plate_type, type_confidence)


@lru_cache(maxsize=1)
def models():
    """Carrega detector e classificador uma única vez e reutiliza-os entre quadros."""
    if not DETECTOR_PATH.is_file() or not CLASSIFIER_PATH.is_file():
        raise FileNotFoundError("Os modelos treinados ainda não estão disponíveis.")
    if not OCR_PATH.is_file():
        raise FileNotFoundError("O OCR nativo ainda não foi compilado.")
    return YOLO(DETECTOR_PATH), YOLO(CLASSIFIER_PATH)


@app.get("/health")
def health():
    """Informa se os pesos e o binário de OCR exigidos estão disponíveis."""
    return {"detector": DETECTOR_PATH.is_file(), "classifier": CLASSIFIER_PATH.is_file(), "ocr": OCR_PATH.is_file()}


@app.post("/recognize")
async def recognize(
    image: UploadFile = File(...), x: float = Form(0), y: float = Form(0),
    width: float = Form(1), height: float = Form(1),
):
    """Valida a imagem, detecta placas e devolve texto, tipo, posição e qualidade."""
    if image.content_type not in {"image/jpeg", "image/png", "image/webp"}:
        raise HTTPException(415, "Envie uma imagem JPEG, PNG ou WebP.")
    content = await image.read(MAX_IMAGE_BYTES + 1)
    if len(content) > MAX_IMAGE_BYTES:
        raise HTTPException(413, "A imagem deve ter no máximo 10 MB.")
    frame = cv2.imdecode(np.frombuffer(content, np.uint8), cv2.IMREAD_COLOR)
    if frame is None:
        raise HTTPException(400, "Imagem inválida.")
    if frame.shape[0] * frame.shape[1] > 20_000_000:
        raise HTTPException(413, "A imagem deve ter no máximo 20 megapixels.")

    try:
        detector, classifier = models()
    except FileNotFoundError as error:
        raise HTTPException(503, str(error)) from error

    frame_height, frame_width = frame.shape[:2]
    region_x = min(max(x, 0), .99)
    region_y = min(max(y, 0), .99)
    region_width = min(max(width, .01), 1 - region_x)
    region_height = min(max(height, .01), 1 - region_y)
    margin = .15
    scan_x, scan_y = max(0, region_x - margin), max(0, region_y - margin)
    scan_right = min(1, region_x + region_width + margin)
    scan_bottom = min(1, region_y + region_height + margin)
    offset_x, offset_y = int(scan_x * frame_width), int(scan_y * frame_height)
    region_right = max(offset_x + 1, int(scan_right * frame_width))
    region_bottom = max(offset_y + 1, int(scan_bottom * frame_height))
    region_frame = frame[offset_y:region_bottom, offset_x:region_right]
    detections = []
    result = detector.predict(region_frame, conf=0.25, max_det=10, verbose=False)[0]
    for xyxy, confidence in zip(result.boxes.xyxy.cpu().tolist(), result.boxes.conf.cpu().tolist()):
        x1, y1, x2, y2 = map(int, xyxy)
        x1, x2 = x1 + offset_x, x2 + offset_x
        y1, y2 = y1 + offset_y, y2 + offset_y
        pad_x, pad_y = int((x2 - x1) * .06), int((y2 - y1) * .12)
        crop = frame[max(0, y1 - pad_y):min(frame_height, y2 + pad_y), max(0, x1 - pad_x):min(frame_width, x2 + pad_x)]
        if crop.size == 0:
            continue
        plate = rectify_plate(crop)
        quality = crop_quality(plate)
        plate_type_result = classifier.predict(plate, verbose=False)[0]
        plate_type = plate_type_result.names[plate_type_result.probs.top1]
        type_confidence = float(plate_type_result.probs.top1conf)
        text, ocr_confidence, plate_type = read_plate(plate, plate_type.upper(), type_confidence)
        detections.append({
            "box": {"x": x1 / frame_width, "y": y1 / frame_height, "width": (x2 - x1) / frame_width, "height": (y2 - y1) / frame_height},
            "confidence": float(confidence), "rawText": text,
            "ocrConfidence": ocr_confidence, "plateType": plate_type.upper(),
            "typeConfidence": type_confidence, "qualityScore": quality,
        })
    return {"detections": detections}
