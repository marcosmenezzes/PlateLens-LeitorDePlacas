# PlateLens — leitor inteligente de placas veiculares

O PlateLens é um sistema local de controle de acesso veicular que captura imagens de uma webcam ou câmera IP, localiza a placa com YOLO, identifica o padrão brasileiro antigo ou Mercosul, lê os caracteres com Apple Vision e registra entradas e saídas em tempo real. O projeto inclui painel web, cadastro de veículos, histórico da portaria, indicadores operacionais e configuração visual da região de captura.

> **Descrição curta para o campo “About” do GitHub:** Controle de acesso veicular com YOLO, OCR Apple Vision, ASP.NET Core, React e registro em tempo real de placas brasileiras.

## Sumário

- [O que o projeto faz](#o-que-o-projeto-faz)
- [Como funciona](#como-funciona)
- [Tecnologias](#tecnologias)
- [Requisitos](#requisitos)
- [Instalação completa](#instalação-completa)
- [Download e preparação dos datasets](#download-e-preparação-dos-datasets)
- [Treinamento dos modelos](#treinamento-dos-modelos)
- [Execução do sistema](#execução-do-sistema)
- [Primeiro uso](#primeiro-uso)
- [Configuração](#configuração)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Verificações](#verificações)
- [Solução de problemas](#solução-de-problemas)
- [Limitações e segurança](#limitações-e-segurança)
- [Licença](#licença)

## O que o projeto faz

- captura continuamente quadros de uma webcam integrada/USB ou de uma câmera IP;
- permite desenhar e salvar a região da imagem em que a passagem deve ser considerada;
- detecta uma ou mais placas em cada quadro com um modelo YOLO;
- classifica a placa como `ANTIGA` ou `MERCOSUL`;
- corrige a perspectiva, melhora o contraste e envia o recorte ao OCR nativo do macOS;
- normaliza e valida placas brasileiras nos formatos `ABC1234` e `ABC1D23`;
- usa confiança, qualidade do recorte, consenso entre quadros e cruzamento da região para reduzir falsos registros;
- cria automaticamente um veículo como `Desconhecido` quando uma nova placa válida é aceita;
- registra entrada ou saída no SQLite e atualiza a interface em tempo real por SSE;
- oferece telas de visão geral, portaria, veículos, monitoramento e estatísticas.

## Como funciona

```text
Webcam do navegador ou stream MJPEG da câmera IP
                    │
                    ▼
           Frontend React/Vite
       reduz e envia um quadro JPEG
                    │
                    ▼
              API ASP.NET Core
       valida o upload e serializa a fila
                    │
                    ▼
          Serviço Python/FastAPI
   YOLO detector → recorte → perspectiva
   → classificador → Apple Vision OCR
                    │
                    ▼
          Regras de negócio .NET
 formato + região + confiança + consenso
          + direção do cruzamento
                    │
                    ▼
       SQLite + atualização SSE do painel
```

O frontend envia um quadro a cada 250 ms quando a análise anterior terminou. A API aceita JPEG, PNG ou WebP de até 10 MB, limita o endpoint de reconhecimento a 300 solicitações por minuto por endereço IP e processa uma inferência por vez. O serviço de visão restringe a imagem a 20 megapixels, detecta até dez placas e devolve posição, texto, tipo e métricas de confiança.

Uma leitura só vira evento depois das validações do backend. Placas desconhecidas exigem consenso entre quadros; placas já cadastradas e com leitura forte podem seguir pelo caminho rápido. Eventos repetidos do mesmo tipo dentro de cinco segundos não são gravados novamente.

Mais detalhes sobre as responsabilidades internas estão em [ARCHITECTURE.md](ARCHITECTURE.md).

## Tecnologias

| Camada | Tecnologia | Responsabilidade |
| --- | --- | --- |
| Interface | React 19 + Vite 7 | câmera, monitoramento, cadastros e dashboards |
| API | ASP.NET Core / .NET 10 | endpoints, validações, regras e orquestração |
| Persistência | Entity Framework Core + SQLite | veículos, câmeras, tentativas e eventos |
| Visão computacional | Python 3.12 + Ultralytics YOLO + OpenCV | detecção, classificação e tratamento do recorte |
| OCR | Apple Vision + Swift | leitura local dos caracteres da placa |
| Tempo real | Server-Sent Events (SSE) | atualização da portaria e dos indicadores |
| Testes de interface | Playwright | verificação ponta a ponta do frontend |

## Requisitos

### Sistema operacional

A execução completa está preparada para **macOS**, porque `vision/apple_ocr.swift` usa o framework Apple Vision e o treinamento usa `mps` por padrão para aproveitar a GPU Apple Silicon.

Linux e Windows podem preparar dados ou treinar com `--device cpu`/CUDA, mas o serviço de reconhecimento não funcionará sem substituir o OCR nativo por outro compatível.

### Ferramentas necessárias

- Git;
- Xcode Command Line Tools, incluindo `swiftc` e `make`;
- Python 3.12;
- SDK do .NET 10;
- Node.js com npm;
- aproximadamente 10 GB livres para dependências, datasets, pesos e execuções de treinamento;
- acesso à internet durante a instalação, o download dos datasets e o primeiro treinamento.

No macOS, instale as ferramentas básicas com:

```bash
xcode-select --install
brew install python@3.12 node
brew install --cask dotnet-sdk
```

Confirme a instalação:

```bash
git --version
swiftc --version
python3.12 --version
dotnet --version
node --version
npm --version
make --version
```

O `dotnet --version` deve começar com `10.`. Se o comando `brew` não existir, instale o [Homebrew](https://brew.sh/) ou instale Python, Node.js e .NET pelos respectivos instaladores oficiais.

## Instalação completa

### 1. Clonar o repositório

```bash
git clone https://github.com/marcosmenezzes/PlateLens-LeitorDePlacas.git
cd PlateLens-LeitorDePlacas
```

Todos os comandos deste README partem da raiz do repositório.

### 2. Instalar as dependências do backend

```bash
dotnet restore PlateLens.slnx
```

### 3. Instalar as dependências do frontend

```bash
cd frontend
npm ci
cd ..
```

Use `npm ci` para instalar exatamente as versões registradas em `frontend/package-lock.json`.

### 4. Criar o ambiente Python

```bash
python3.12 -m venv vision/.venv
vision/.venv/bin/python -m pip install --upgrade pip
vision/.venv/bin/pip install -r vision/requirements.txt
```

As dependências Python ficam isoladas em `vision/.venv` e não são versionadas.

### 5. Baixar, preparar e treinar os modelos

Os pesos `.pt` não são armazenados no Git. Siga as duas próximas seções para gerar:

```text
models/plate.pt       detector da região da placa
models/plate-type.pt  classificador ANTIGA/MERCOSUL
```

## Download e preparação dos datasets

O treinamento usa dois datasets publicados no Roboflow Universe sob licença CC BY 4.0:

1. [car plate](https://universe.roboflow.com/school-nyesk/car-plate-runio), versão 3, para detectar a região da placa;
2. [Mercosul or not classification](https://universe.roboflow.com/projects-5mxpc/mercosul-or-not-classification), versão 1, para classificar placas antigas e Mercosul.

### 1. Baixar o detector

Na página do dataset **car plate**:

1. abra a versão 3;
2. clique em **Download Dataset**;
3. selecione o formato **YOLO26**;
4. escolha o download como arquivo ZIP;
5. mantenha o arquivo com o nome `car plate.v3i.yolo26.zip`.

### 2. Baixar o classificador

Na página do dataset **Mercosul or not classification**:

1. abra a versão 1;
2. clique em **Download Dataset**;
3. selecione o formato de classificação **Multiclass**;
4. escolha o download como arquivo ZIP;
5. mantenha o arquivo com o nome `Mercosul or not classification.v1i.multiclass.zip`.

Algumas opções do Roboflow podem exigir uma conta gratuita. Não extraia os arquivos manualmente.

### 3. Colocar os ZIPs no local esperado

Os dois arquivos devem estar diretamente na pasta `Downloads` do usuário:

```text
~/Downloads/car plate.v3i.yolo26.zip
~/Downloads/Mercosul or not classification.v1i.multiclass.zip
```

Confira os nomes, incluindo espaços e letras maiúsculas:

```bash
ls -lh \
  "$HOME/Downloads/car plate.v3i.yolo26.zip" \
  "$HOME/Downloads/Mercosul or not classification.v1i.multiclass.zip"
```

Se o navegador acrescentar um sufixo ao nome, renomeie o arquivo antes de continuar. O script usa exatamente os dois caminhos acima.

### 4. Preparar os dados

Na raiz do repositório, execute:

```bash
vision/.venv/bin/python vision/prepare_data.py
```

O script:

- recria `vision/data/detector` e `vision/data/classifier`;
- extrai os conjuntos `train`, `valid` e `test`;
- normaliza o detector para uma única classe chamada `plate`;
- organiza o classificador no formato de pastas esperado pelo Ultralytics;
- gera `vision/data/detector/data.yaml` com caminhos locais;
- confirma ao final 622 imagens de treino do detector e 1.129 imagens de treino do classificador.

Saída esperada:

```text
Datasets preparados em .../PlateLens-LeitorDePlacas/vision/data
```

> Executar `prepare_data.py` novamente apaga e recria somente `vision/data/detector` e `vision/data/classifier`. Os ZIPs originais em `~/Downloads` não são alterados.

A estrutura resultante será semelhante a:

```text
vision/data/
├── detector/
│   ├── data.yaml
│   ├── train/{images,labels}/
│   ├── valid/{images,labels}/
│   └── test/{images,labels}/
└── classifier/
    ├── train/{ANTIGA,MERCOSUL}/
    ├── val/{ANTIGA,MERCOSUL}/
    └── test/{ANTIGA,MERCOSUL}/
```

## Treinamento dos modelos

Os treinamentos podem ser executados um depois do outro. No primeiro uso, o Ultralytics baixa automaticamente os pesos-base `yolo26n.pt` e `yolo26n-cls.pt`.

### 1. Treinar o detector

```bash
vision/.venv/bin/python vision/train.py detector
```

Configuração padrão: imagens de 640 px, lote 16, até 40 épocas, parada antecipada após oito épocas sem melhora e dispositivo `mps`.

O melhor peso será salvo em:

```text
vision/runs/detector/weights/best.pt
```

### 2. Treinar o classificador

```bash
vision/.venv/bin/python vision/train.py classifier
```

Configuração padrão: imagens de 224 px, lote 16, até 30 épocas, parada antecipada após oito épocas sem melhora e dispositivo `mps`.

O melhor peso será salvo em:

```text
vision/runs/classifier/weights/best.pt
```

### Ajustar épocas ou dispositivo

Use `--epochs` para reduzir ou ampliar o treinamento:

```bash
vision/.venv/bin/python vision/train.py detector --epochs 60
vision/.venv/bin/python vision/train.py classifier --epochs 40
```

Em um Mac sem suporte a MPS, use CPU:

```bash
vision/.venv/bin/python vision/train.py detector --device cpu
vision/.venv/bin/python vision/train.py classifier --device cpu
```

O treinamento em CPU pode demorar consideravelmente mais. Em uma máquina compatível com CUDA, passe o dispositivo aceito pelo Ultralytics, por exemplo `--device 0`.

### Acompanhar o progresso

Depois que os dois processos já tiverem criado seus arquivos `results.csv`, execute em outro terminal:

```bash
make watch-training
```

Ou acompanhe um treinamento específico:

```bash
tail -f vision/runs/detector/results.csv
tail -f vision/runs/classifier/results.csv
```

Pressione `Ctrl+C` para parar apenas o acompanhamento; o treinamento continua no terminal em que foi iniciado.

### 3. Instalar os pesos treinados

Copie os melhores pesos para a pasta usada pelo serviço:

```bash
cp vision/runs/detector/weights/best.pt models/plate.pt
cp vision/runs/classifier/weights/best.pt models/plate-type.pt
```

Valide os arquivos:

```bash
ls -lh models/plate.pt models/plate-type.pt
```

Esses arquivos são ignorados pelo Git porque são binários grandes. Em outra instalação, treine novamente ou copie pesos compatíveis para esses mesmos caminhos e nomes.

## Execução do sistema

### Opção recomendada: iniciar tudo junto

Na raiz do repositório:

```bash
make run
```

Esse comando:

1. compila `vision/apple_ocr.swift` como `vision/apple-ocr` quando necessário;
2. inicia o serviço de visão em `http://127.0.0.1:8001`;
3. inicia a API em `http://127.0.0.1:5055`;
4. inicia o frontend em `http://127.0.0.1:5173`;
5. encerra os três processos quando você pressiona `Ctrl+C`.

Abra no navegador:

```text
http://127.0.0.1:5173/
```

### Opção alternativa: um serviço por terminal

Terminal 1 — visão:

```bash
make run-vision
```

Terminal 2 — API:

```bash
make run-back
```

Terminal 3 — frontend:

```bash
make run-front
```

Essa opção facilita a leitura dos logs de cada processo.

### Conferir a disponibilidade

Com o sistema iniciado, execute em outro terminal:

```bash
curl http://127.0.0.1:8001/health
curl http://127.0.0.1:5055/api/vision/model
```

O primeiro endpoint deve informar `true` para `detector`, `classifier` e `ocr`. O segundo deve retornar `"available": true`.

Na primeira inicialização, a API cria automaticamente `platelens.db`, prepara as tabelas e cadastra a fonte `Câmera nativa`. O arquivo SQLite é local e ignorado pelo Git.

## Primeiro uso

### Usar a webcam do computador

1. abra `http://127.0.0.1:5173/monitoring`;
2. autorize o navegador a acessar a câmera;
3. mantenha `Câmera nativa` selecionada;
4. arraste ou redimensione o retângulo **REGIÃO DE CAPTURA** para a área por onde a placa passará;
5. posicione uma placa visível, iluminada e aproximadamente horizontal diante da câmera;
6. aguarde o consenso das leituras e o cruzamento da região;
7. acompanhe o evento em **Portaria** e edite o veículo criado como `Desconhecido` na tela **Veículos**.

O acesso à webcam funciona em contexto seguro. `127.0.0.1` é aceito pelos navegadores modernos; ao publicar o frontend em outro endereço, configure HTTPS.

### Usar uma câmera IP

1. a câmera deve possuir IPv4 privado, como `192.168.1.20`;
2. ela deve fornecer um stream de imagem ou MJPEG em `http://IP:PORT/video`;
3. na tela **Monitoramento**, informe nome, endereço IPv4 e porta;
4. clique em **Cadastrar câmera** e depois em **Selecionar**;
5. ajuste e salve a região de captura.

Por segurança, a API rejeita endereços públicos, hostnames e destinos fora das faixas IPv4 privadas aceitas. Autenticação da câmera, RTSP e caminhos diferentes de `/video` ainda não são suportados.

### Telas disponíveis

| Rota | Função |
| --- | --- |
| `/` | resumo do movimento recente e indicadores do dia/semana |
| `/portaria` | histórico atualizado automaticamente de entradas e saídas |
| `/vehicles` | cadastro, busca, edição e exclusão de veículos |
| `/monitoring` | câmera ao vivo, reconhecimento, fontes e região de captura |
| `/analytics` | períodos, horários de pico, frequência e permanência média |

## Configuração

As configurações principais ficam em `src/PlateLens.WebApi/appsettings.json`:

| Chave | Padrão | Uso |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `Data Source=platelens.db` | caminho do banco SQLite |
| `Frontend:Origin` | `http://127.0.0.1:5173` | origem permitida pelo CORS |
| `Vision:ServiceUrl` | `http://127.0.0.1:8001` | endereço interno do serviço Python |
| `Vision:ModelPath` | `../../models/plate.pt` | detector consultado pela API |
| `Vision:ClassifierPath` | `../../models/plate-type.pt` | classificador consultado pela API |
| `Vision:OcrPath` | `../../vision/apple-ocr` | binário Swift consultado pela API |
| `Vision:MinConfidence` | `0.35` | confiança combinada mínima no backend |

O serviço Python também aceita os caminhos por variáveis de ambiente:

```bash
PLATELENS_DETECTOR=/caminho/detector.pt \
PLATELENS_CLASSIFIER=/caminho/classificador.pt \
PLATELENS_OCR=/caminho/apple-ocr \
make run-vision
```

Não altere apenas as variáveis do Python se quiser que `GET /api/vision/model` reflita os mesmos arquivos; nesse caso, ajuste também as chaves `Vision` da API.

## Estrutura do projeto

```text
PlateLens-LeitorDePlacas/
├── src/
│   ├── PlateLens.Domain/       entidades e regras puras de negócio
│   ├── PlateLens.Infra.Data/   Entity Framework Core e SQLite
│   └── PlateLens.WebApi/       controllers, serviços e configuração HTTP
├── tests/PlateLens.Checks/     verificações executáveis do domínio e backend
├── frontend/                   aplicação React, estilos e testes Playwright
├── vision/                     preparo de dados, treino, FastAPI, OpenCV e OCR
├── models/                     destino dos dois pesos treinados
├── ARCHITECTURE.md             decisões e fluxo entre as camadas
├── Makefile                    comandos para iniciar os serviços
└── PlateLens.slnx              solução .NET
```

Arquivos gerados localmente e ignorados pelo Git:

- `vision/.venv/`: ambiente Python;
- `vision/data/`: datasets preparados;
- `vision/runs/`: métricas e pesos de treinamento;
- `vision/apple-ocr`: binário Swift compilado;
- `models/*.pt`: pesos instalados;
- `frontend/node_modules/`: dependências JavaScript;
- `wwwroot/`: build do frontend;
- `*.db`, `*.db-shm` e `*.db-wal`: banco SQLite e arquivos auxiliares.

## Verificações

### Backend e regras de negócio

```bash
dotnet build PlateLens.slnx
dotnet run --project tests/PlateLens.Checks
```

Saída esperada da segunda etapa:

```text
Checks passed.
```

### Funções de OCR e processamento de imagem

```bash
cd vision
.venv/bin/python test_text.py
cd ..
```

Saída esperada:

```text
OCR helpers: OK
```

### Frontend

Instale o navegador do Playwright somente na primeira execução:

```bash
cd frontend
npx playwright install chromium
npm run build
npm run test:e2e
cd ..
```

## Solução de problemas

### `FileNotFoundError` ao preparar os dados

Confira se os dois ZIPs estão em `~/Downloads` e têm exatamente os nomes documentados. O script não procura em outras pastas.

### Modelos ausentes ou resposta HTTP 503

Confirme que estes arquivos existem:

```bash
ls -lh models/plate.pt models/plate-type.pt vision/apple-ocr
```

Se apenas o OCR estiver ausente, compile-o:

```bash
make vision/apple-ocr
```

### `swiftc: command not found`

Instale as Xcode Command Line Tools:

```bash
xcode-select --install
```

### Erro relacionado a `mps` no treinamento

Use CPU:

```bash
vision/.venv/bin/python vision/train.py detector --device cpu
vision/.venv/bin/python vision/train.py classifier --device cpu
```

### A câmera do navegador não abre

- confirme a permissão de câmera nas configurações do navegador e do macOS;
- use exatamente `http://127.0.0.1:5173`, não um endereço HTTP da rede local;
- feche outros programas que estejam usando a câmera;
- clique em **Tentar ativar câmera** depois de corrigir a permissão.

### A câmera IP não conecta

- use um IPv4 privado, não hostname ou IP público;
- confirme que `http://IP:PORT/video` abre no navegador;
- confirme que a resposta é uma imagem ou stream `multipart/x-mixed-replace`;
- verifique se o computador e a câmera estão na mesma rede.

### Uma placa é detectada, mas não é registrada

Verifique na tela de monitoramento:

- se a placa intersecta a região configurada;
- se o texto segue `ABC1234` ou `ABC1D23`;
- se a imagem tem foco, luz e tamanho suficientes;
- se a confiança e a qualidade ultrapassam os limites;
- se houve quadros suficientes para formar consenso;
- se um evento igual não acabou de ser gravado nos últimos cinco segundos.

### Uma porta já está em uso

O projeto usa as portas `5173`, `5055` e `8001`. Descubra o processo responsável com:

```bash
lsof -iTCP:5173 -sTCP:LISTEN
lsof -iTCP:5055 -sTCP:LISTEN
lsof -iTCP:8001 -sTCP:LISTEN
```

Encerre o processo apropriado ou ajuste de forma consistente o Makefile, o proxy do Vite e as configurações da API.

### Recomeçar com um banco vazio

Com os serviços encerrados, mova o banco atual para um backup:

```bash
mv platelens.db platelens.db.backup
```

Na próxima inicialização, a API criará um banco novo. O backup preserva os dados anteriores.

## Limitações e segurança

- a autenticação e a autorização estão temporariamente desativadas para validação local;
- qualquer cliente que alcance a API pode consultar ou alterar os dados;
- os modelos identificam padrões visuais, mas não comprovam autenticidade, propriedade ou autorização do veículo;
- a qualidade depende do dataset, iluminação, distância, ângulo, movimento e câmera;
- o OCR atual exige macOS;
- a câmera IP aceita somente IPv4 privado e stream em `/video` sem autenticação;
- o banco usa `EnsureCreated`; antes de distribuir atualizações de schema, o projeto deve adotar migrations;
- o processamento de inferência é serializado e foi dimensionado para validação local, não para múltiplas câmeras em alta escala.

> **Não exponha esta versão na internet ou em uma rede não confiável.** Antes de produção, implemente autenticação, autorização, HTTPS, gestão de segredos, migrations, política de retenção, backup e monitoramento. Dados de placas podem ser dados pessoais conforme o contexto; verifique a base legal, a finalidade e as obrigações aplicáveis antes de coletá-los.

## Datasets e atribuição

- [car plate](https://universe.roboflow.com/school-nyesk/car-plate-runio), fornecido por um usuário do Roboflow — licença CC BY 4.0;
- [Mercosul or not classification](https://universe.roboflow.com/projects-5mxpc/mercosul-or-not-classification), fornecido por um usuário do Roboflow — licença CC BY 4.0.

Consulte as páginas originais para confirmar versões, termos e atribuições antes de redistribuir dados ou modelos derivados.

## Licença

O código do PlateLens é distribuído sob a licença MIT. Consulte [LICENSE](LICENSE).
