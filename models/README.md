# Modelos de placas

O treinamento gera os pesos usados pelo serviço:

- `models/plate.pt`: detector da região da placa;
- `models/plate-type.pt`: classificador `ANTIGA` ou `MERCOSUL`.

O Apple Vision OCR lê diretamente a faixa de caracteres do recorte localizado e retificado pelo detector. A API mostra a disponibilidade em `GET /api/vision/model`.

A captura só será aceita quando a bounding box cruzar a região configurada, a confiança superar o limite e o OCR produzir uma placa brasileira sintaticamente válida.
