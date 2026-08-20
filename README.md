# PlateLens

Controle de acesso veicular com captura contínua, reconhecimento de placas e registro de entradas e saídas.

Consulte [ARCHITECTURE.md](ARCHITECTURE.md) para entender as responsabilidades das pastas e o fluxo completo de uma captura.

## Executar

Na primeira vez, prepare o serviço de visão:

```bash
brew install python@3.12
python3.12 -m venv vision/.venv
vision/.venv/bin/pip install -r vision/requirements.txt
```

Subir o projeto inteiro:

```bash
make run
```

Ou executar cada parte em um terminal:

```bash
make run-vision
make run-back
make run-front
```

Para acompanhar um novo treinamento:

```bash
make watch-training
```

Abra `http://127.0.0.1:5173/`. A autenticação está temporariamente desativada para a validação local. A câmera nativa inicia ao autorizar a permissão do navegador e tenta reconhecer placas continuamente. Câmeras IP e regiões ficam persistidas no SQLite quando a API está ativa; sem ela, a interface entra em modo demonstração local.

Consulte [vision/README.md](vision/README.md) para reproduzir os treinamentos.

## Verificar

```bash
dotnet build PlateLens.slnx
dotnet run --project tests/PlateLens.Checks
cd vision && .venv/bin/python test_text.py
cd frontend && npm run build && npm run test:e2e
```

> Não exponha esta versão na internet ou em outra rede: sem autenticação, qualquer cliente que alcance a API pode consultar e alterar os dados.
