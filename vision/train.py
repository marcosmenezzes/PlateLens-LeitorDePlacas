from argparse import ArgumentParser
from pathlib import Path
from ultralytics import YOLO

ROOT = Path(__file__).resolve().parent


def main() -> None:
    """Seleciona a tarefa e inicia o treinamento YOLO com os dados preparados."""
    parser = ArgumentParser()
    parser.add_argument("task", choices=("detector", "classifier"))
    parser.add_argument("--epochs", type=int)
    parser.add_argument("--device", default="mps")
    args = parser.parse_args()

    if args.task == "detector":
        model, data, size, epochs = "yolo26n.pt", ROOT / "data/detector/data.yaml", 640, args.epochs or 40
    else:
        model, data, size, epochs = "yolo26n-cls.pt", ROOT / "data/classifier", 224, args.epochs or 30

    YOLO(model).train(
        data=str(data), epochs=epochs, imgsz=size, batch=16, device=args.device, workers=0,
        patience=8, project=str(ROOT / "runs"), name=args.task, exist_ok=True,
    )


if __name__ == "__main__":
    main()
