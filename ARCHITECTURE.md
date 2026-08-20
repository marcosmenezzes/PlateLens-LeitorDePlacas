# Arquitetura do PlateLens

O projeto usa Clean Architecture de forma proporcional ao tamanho atual: regras de negócio ficam no centro, persistência fica isolada e a API coordena os casos de uso. As dependências apontam para o domínio.

```text
src/
├── PlateLens.Domain/       entidades e regras puras de negócio
├── PlateLens.Infra.Data/   SQLite, Entity Framework e mapeamentos
└── PlateLens.WebApi/       endpoints, casos de uso e composição da aplicação
tests/
└── PlateLens.Checks/       verificações executáveis do domínio e backend
frontend/                   interface React e captura da câmera do navegador
vision/                     detector YOLO, classificador e OCR nativo
models/                     pesos treinados usados pelo serviço de visão
```

## Direção das dependências

```text
PlateLens.WebApi ──> PlateLens.Infra.Data ──> PlateLens.Domain
        └──────────────────────────────────> PlateLens.Domain
```

`PlateLens.Domain` não conhece banco, HTTP, React ou YOLO. `PlateLens.Infra.Data` conhece somente o domínio e o Entity Framework. `PlateLens.WebApi` é a borda HTTP e reúne os componentes no processo principal.

Uma camada `Application` separada ainda não foi criada. No tamanho atual ela produziria interfaces com uma única implementação sem criar uma fronteira real. Os casos de uso estão nos serviços da API e podem ser extraídos quando existir outro host, outro adaptador ou a necessidade concreta de testar esses casos sem a API.

## Fluxo de uma placa

```text
Câmera no navegador
  -> VisionController recebe o quadro e coloca-o na fila
  -> serviço Python recorta a placa, classifica e executa OCR
  -> regras do Domain validam formato, consenso, região e cruzamento
  -> AccessService grava veículo, tentativa e entrada/saída no SQLite
  -> RealtimeService avisa a interface por SSE
```

O frontend mostra o resultado; a decisão e a gravação permanecem no backend. Assim, fechar ou atualizar a tela não muda as regras registradas no banco.

## Inicialização da API

O [Program.cs](src/PlateLens.WebApi/Program.cs) só descreve a ordem de inicialização:

1. `AddPlateLens` registra banco, limite de requisições, CORS e serviços.
2. `AddControllers` configura os endpoints e a conversão de enums para JSON.
3. `UsePlateLensPipeline` monta tratamento de erros, CORS, limite e rotas.
4. `InitializeDatabaseAsync` cria e compatibiliza o SQLite antes de aceitar uso.

Cada etapa está em `src/PlateLens.WebApi/Configuration`, evitando um `Program.cs` longo e difícil de explicar.

## Responsabilidades principais

- `Controllers`: recebem HTTP, validam o contrato e delegam o trabalho.
- `Services`: executam casos de uso como cadastrar veículo, registrar acesso e calcular indicadores.
- `Rules`: decisões de negócio independentes de infraestrutura.
- `AppDbContext`: mapeia e persiste entidades; datas são normalizadas para UTC ao salvar.
- `vision/app.py`: valida o arquivo, detecta a placa, melhora o recorte, executa OCR e devolve candidatos.
- `frontend/src/pages`: apresenta os dados e envia quadros; não decide o que deve ser persistido.

## Segurança temporária

A autenticação foi removida apenas para facilitar a validação local. A API continua validando entradas, limitando uploads e restringindo cadastro de câmera a IPv4 privado, mas qualquer pessoa com acesso à porta `5055` pode alterar os dados. Autenticação e autorização devem voltar antes de implantação ou exposição à rede.
