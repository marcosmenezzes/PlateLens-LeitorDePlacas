import numpy as np

from app import choose_ocr_text, crop_quality, fit_plate, normalize_text, prepare_ocr_image


def test_text_helpers():
    assert normalize_text("abc-1d23") == "ABC1D23"
    text, confidence, plate_type = choose_ocr_text([
        (None, "BRASIL", 0.99),
        (None, "ABC1D23", 0.91),
    ], "MERCOSUL")
    assert (text, round(confidence, 2), plate_type) == ("ABC1D23", 0.92, "MERCOSUL")
    assert choose_ocr_text([(None, "ABC1234", .9)], "MERCOSUL", .99)[2] == "ANTIGA"
    assert fit_plate("OZL44656", "ANTIGA") == ("OZL4465", 0)
    assert fit_plate("RFA4158", "MERCOSUL") == ("RFA4I58", 1)
    crop = np.full((80, 320, 3), 128, dtype=np.uint8)
    assert 0 <= crop_quality(crop) <= 1
    assert prepare_ocr_image(crop).shape[0] > crop.shape[0]


if __name__ == "__main__":
    test_text_helpers()
    print("OCR helpers: OK")
