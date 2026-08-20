# Treinamento e OCR

Os ZIPs permanecem em `~/Downloads`; `prepare_data.py` cria cópias normalizadas em `vision/data`, ignorada pelo Git.

```bash
vision/.venv/bin/python vision/prepare_data.py
vision/.venv/bin/python vision/train.py detector
vision/.venv/bin/python vision/train.py classifier
cp vision/runs/detector/weights/best.pt models/plate.pt
cp vision/runs/classifier/weights/best.pt models/plate-type.pt
```

Acompanhar os dois treinamentos em tempo real:

```bash
tail -f vision/runs/{detector,classifier}/results.csv
```

Encerre o acompanhamento com `Ctrl+C`; isso não interrompe os treinamentos.

Executar o OCR e os modelos somente no computador local:

```bash
make run-vision
```

O comando compila o leitor nativo `Apple Vision`: YOLO detecta e recorta a placa, OpenCV corrige a perspectiva, o OCR lê somente a faixa de caracteres e o backend valida e grava o evento.

Datasets CC BY 4.0: [car plate](https://universe.roboflow.com/school-nyesk/car-plate-runio) e [Mercosul or not classification](https://universe.roboflow.com/projects-5mxpc/mercosul-or-not-classification).
