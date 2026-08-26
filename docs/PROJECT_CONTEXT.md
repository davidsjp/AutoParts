# AutoParts Project Context

Este documento resume o objetivo, a arquitetura e os fluxos principais do projeto
para facilitar onboarding de outra pessoa ou IA.

## Objetivo

AutoParts e uma aplicacao ASP.NET Core para montar e manter um catalogo comercial
de autopecas, com foco atual em pecas BMW identificadas por codigo OEM.

O sistema centraliza:

- cadastro local de veiculos, pecas e compatibilidades;
- importacao de pecas e aplicacoes a partir de catalogos externos, principalmente
  BMV.parts e dados copiados do RealOEM;
- normalizacao de nomes tecnicos para titulos mais comerciais em portugues;
- geracao de sugestoes de anuncio, descricao e imagens para marketplace;
- exportacao de catalogo em JSON e TSV.

## Stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core com SQLite local
- Swagger para exploracao da API
- Frontend estatico em `src/AutoParts.Api/wwwroot`
- xUnit para testes de API
- Integracoes HTTP com BMV.parts, Wikimedia Commons e OpenAI

## Como rodar

```powershell
dotnet restore
dotnet ef database update --project src/AutoParts.Api
dotnet run --project src/AutoParts.Api --urls http://localhost:5080
```

Endpoints uteis:

- `GET /health`
- `GET /swagger`
- `GET /api/parts/catalog`
- `GET /api/parts/catalog.tsv`
- `GET /api/external-catalog/search?q=oil`
- `GET /api/external-catalog/preview/{oem}`
- `POST /api/external-catalog/import/{oem}`
- `POST /api/external-catalog/import-car/{externalCarId}`

## Modelo de dominio

### Vehicle

Representa um veiculo no catalogo local. Alem dos campos basicos de fabricante,
modelo, chassi, motor e ano, pode guardar dados especificos de catalogo:

- `SerialNumber`: usado para identificar o veiculo importado do BMV.parts
  no formato `BMV{id}`;
- `Market`: mercado do veiculo;
- `TypeCode`: codigo tecnico do tipo BMW;
- `Compatibilities`: relacao com pecas aplicaveis.

### Part

Representa uma peca vendavel ou catalogada. A chave comercial e
`OemPartNumber`, que deve permanecer normalizada e unica.

Campos importantes:

- `Description`: titulo/nome comercial da peca;
- `Category`: categoria de catalogo ou uma categoria de exclusao como
  `Ignorar - fixadores e mangueiras`;
- `Source`: origem dos dados, por exemplo `BMV.parts` ou `RealOEM.com`;
- `KeywordGroup`: termos de busca para marketplace;
- `Applications`: texto com aplicacoes/compatibilidades resumidas;
- `SuggestedValue`: preco sugerido opcional.

### PartCompatibility

Tabela de ligacao entre peca e veiculo, com intervalo de producao e observacoes.
O indice unico evita duplicar a mesma peca no mesmo veiculo para a mesma data de
inicio.

### ImportRun

Log operacional de importacoes. Cada importacao registra origem, status, datas,
quantidade processada e eventual erro.

## Arquitetura

### Program.cs

Configura a aplicacao:

- controllers, ProblemDetails e tratamento global de excecoes;
- Swagger;
- SQLite via connection string `AutoParts`;
- servicos de dominio;
- clientes HTTP para BMV.parts, Wikimedia e OpenAI;
- arquivos estaticos da UI;
- comandos administrativos executados por argumentos de CLI.

Comandos administrativos existentes:

- `--normalize-legacy-categories`
- `--apply-market-name-fixes`
- `--mark-ignored-catalog-items`
- `--normalize-market-part-names`
- `--curate-imported-parts-by-vehicle`
- `--delete-ignored-catalog-items`

Esses comandos alteram dados locais diretamente. Use com backup do SQLite quando
o banco tiver valor operacional.

### Data

`AutoPartsDbContext` define os `DbSet`s e indices principais:

- VIN unico em veiculos;
- serial de catalogo unico em veiculos;
- OEM unico em pecas;
- compatibilidade unica por peca, veiculo e inicio de producao.

### Controllers

- `VehiclesController`: CRUD basico de veiculos.
- `PartsController`: CRUD de pecas, consulta por OEM, compatibilidades e
  exportacao de catalogo TSV.
- `ExternalCatalogController`: busca, preview e importacao a partir de BMV.parts.
- `VehicleCatalogController`: endpoints de leitura para a tela de selecao de
  veiculos e pecas por veiculo.
- `ListingsController`: gera amostra, sugestao de IA, descricao local e imagens
  para anuncios.
- `RealoemImportController`: importa manualmente dados copiados do RealOEM.

### Services

- `VehicleService`: regras basicas de cadastro de veiculos.
- `PartService`: regras basicas de cadastro de pecas e compatibilidades.
- `ExternalCatalogService`: orquestra importacao de pecas/veiculos do BMV.parts,
  normalizacao de nomes e criacao de compatibilidades.
- `MarketPartNameNormalizer`: transforma nomes de catalogo em termos mais
  comerciais e marca itens pouco vendaveis para ignorar.
- `OpenAiPartTranslationService`: usa Responses API para traduzir e normalizar
  dados externos para portugues brasileiro.
- `OpenAiListingGenerationService`: usa Responses API para sugerir titulo,
  palavras-chave, aplicacoes, preco e nota de verificacao.
- `TemplateListingDescriptionService`: gera descricao local sem IA.
- `OpenAiListingImageService`: gera ou reutiliza imagens de anuncio.
- `WikimediaVehicleImageService`: busca imagens de referencia por chassi/modelo.

### Clients

`PartsCatalogClient` encapsula chamadas ao BMV.parts:

- busca textual;
- consulta por OEM;
- consulta de carro externo;
- paginacao de todas as pecas de um carro.

## Fluxos principais

### Importar uma peca por OEM via BMV.parts

1. `ExternalCatalogController.Import` recebe o OEM.
2. `ExternalCatalogService.ImportByOemAsync` cria um `ImportRun`.
3. `PartsCatalogClient.FindByOemNumberAsync` busca a peca externa.
4. `OpenAiPartTranslationService` traduz titulo, categoria, palavras-chave e
   aplicacoes.
5. O servico cria ou atualiza a `Part`.
6. Veiculos externos associados sao criados quando nao existem.
7. `PartCompatibility` liga a peca aos veiculos importados.
8. O `ImportRun` e finalizado como `Completed` ou `Failed`.

### Importar catalogo inteiro de um veiculo BMV.parts

1. `POST /api/external-catalog/import-car/{externalCarId}` recebe dados locais
   do veiculo e intervalo de compatibilidade.
2. O carro externo e suas pecas sao carregados.
3. Pecas sem OEM valido ou sem descricao sao descartadas.
4. O veiculo local e criado ou reutilizado pelo `SerialNumber`.
5. Pecas novas sao criadas com nome normalizado.
6. Fixadores, mangueiras e itens semelhantes podem ser marcados como ignorados.
7. Compatibilidades sao criadas para o veiculo.

### Curar pecas importadas por veiculo

O comando abaixo percorre os veiculos em ordem alfabetica por fabricante, modelo,
chassi e motor. Para cada veiculo, ele carrega as pecas BMV.parts associadas,
ordena alfabeticamente por nome da peca, marca fixadores/mangueiras como
ignorados e traduz o restante em lotes com OpenAI.

```powershell
dotnet run --project src/AutoParts.Api -- --curate-imported-parts-by-vehicle --dry-run
dotnet run --project src/AutoParts.Api -- --curate-imported-parts-by-vehicle --translation-batch-size=50
```

Detalhes importantes:

- `--dry-run` mostra o que seria ignorado/traduzido sem salvar alteracoes.
- Pecas repetidas em mais de um veiculo sao processadas apenas uma vez.
- Itens classificados como `Ignorar - fixadores e mangueiras` ficam fora da
  vitrine comercial, mas continuam no banco ate exclusao confirmada.
- Para remover fisicamente os ignorados, rode primeiro o preview e depois a
  confirmacao explicita:

```powershell
dotnet run --project src/AutoParts.Api -- --delete-ignored-catalog-items
dotnet run --project src/AutoParts.Api -- --delete-ignored-catalog-items --confirm-delete
```

### Gerar anuncio

1. `GET /api/listings/{oem}` monta uma amostra local com dados da peca e
   compatibilidades.
2. `POST /api/listings/{oem}/generate` pede uma sugestao estruturada para a
   OpenAI. Se a origem nao for `RealOEM.com`, o prompt obriga status
   `requer_conferencia`.
3. `POST /api/listings/{oem}/description` gera uma descricao local padronizada.
4. `POST /api/listings/{oem}/images` retorna imagens aprovadas/cached ou gera
   novas imagens com OpenAI.

## Configuracao

`appsettings.json` define:

- `ConnectionStrings:AutoParts`: caminho do SQLite;
- `PartsCatalog:BaseUrl`: base do BMV.parts;
- `OpenAI:Model`: modelo de texto para Responses API.

A chave da OpenAI deve vir de `OpenAI:ApiKey` nos user-secrets ou da variavel
`OPENAI_API_KEY`.

Exemplo:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/AutoParts.Api
```

## Testes

```powershell
dotnet test
```

Os testes atuais validam:

- endpoint de saude;
- criacao e busca de peca por OEM;
- rejeicao de peca invalida.

## Cuidados para futuras alteracoes

- Nao inventar compatibilidades. Compatibilidade comercial deve vir de fonte
  tecnica, preferencialmente RealOEM por VIN/type code.
- Preservar OEM, chassis, codigos de motor, anos e type codes exatamente.
- `Source` influencia confianca: `RealOEM.com` e tratado como fonte mais forte
  para publicacao; `BMV.parts` exige conferencia.
- Antes de rodar comandos administrativos que alteram muitas linhas, fazer backup
  do SQLite.
- O frontend depende dos contratos JSON dos controllers; mudancas em records de
  resposta podem quebrar a UI.
- Imagens geradas ficam em `wwwroot/generated/{OEM}` e podem gerar custo se
  `regenerate=true`.
- O arquivo local `autoparts.db`, se presente, e dado operacional e nao deve ser
  editado manualmente.
