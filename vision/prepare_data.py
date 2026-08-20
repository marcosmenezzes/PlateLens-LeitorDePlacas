from pathlib import Path
from zipfile import ZipFile
import shutil

ROOT = Path(__file__).resolve().parent
DATA = ROOT / "data"
DETECTOR_ZIP = Path.home() / "Downloads/car plate.v3i.yolo26.zip"
CLASSIFIER_ZIP = Path.home() / "Downloads/Mercosul or not classification.v1i.multiclass.zip"


def prepare_detector() -> None:
    """Extrai e normaliza o dataset usado para localizar placas na imagem."""
    target = DATA / "detector"
    shutil.rmtree(target, ignore_errors=True)
    with ZipFile(DETECTOR_ZIP) as archive:
        for name in archive.namelist():
            if name.startswith(("train/", "valid/", "test/")):
                archive.extract(name, target)

    # A detector needs one object class. The original class 0 marks blurred or
    # missing plates, so those boxes become negative examples; real plates become 0.
    for label in target.glob("*/labels/*.txt"):
        lines = ["0 " + line[2:] for line in label.read_text().splitlines() if line.startswith("1 ")]
        label.write_text("\n".join(lines) + ("\n" if lines else ""))

    (target / "data.yaml").write_text(
        f"path: {target}\ntrain: train/images\nval: valid/images\ntest: test/images\nnames:\n  0: plate\n"
    )


def prepare_classifier() -> None:
    """Separa imagens por tipo para treinar o classificador Mercosul/antiga."""
    target = DATA / "classifier"
    shutil.rmtree(target, ignore_errors=True)
    split_names = {"train": "train", "valid": "val", "test": "test"}
    with ZipFile(CLASSIFIER_ZIP) as archive:
        for info in archive.infolist():
            path = Path(info.filename)
            if info.is_dir() or len(path.parts) != 2 or path.suffix.lower() not in {".jpg", ".jpeg", ".png"}:
                continue
            split = split_names.get(path.parts[0])
            plate_type = path.name.split("-", 1)[0].upper()
            if not split or plate_type not in {"ANTIGA", "MERCOSUL"}:
                continue
            destination = target / split / plate_type / path.name
            destination.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info) as source, destination.open("wb") as output:
                shutil.copyfileobj(source, output)


if __name__ == "__main__":
    prepare_detector()
    prepare_classifier()
    assert len(list((DATA / "detector/train/images").glob("*"))) == 622
    assert len(list((DATA / "classifier/train").glob("*/*"))) == 1129
    print("Datasets preparados em", DATA)
