# Workoutly — PostgreSQL e Render

Versão preparada a partir do projeto enviado, com API ASP.NET Core .NET 10 e aplicativo MAUI.

**Comece por [INICIAR-RENDER.md](INICIAR-RENDER.md).** O banco já existente no Render é preservado. O deploy publica somente a API; o aplicativo é compilado e instalado separadamente.

A API usa Npgsql 10.0.3 e recebe a conexão por `DATABASE_URL` (URL interna do Render) ou `ConnectionStrings__Workoutly` (formato Npgsql; tem prioridade quando preenchida). Não há credenciais neste pacote. Não há migração automática no startup.

`Dockerfile` compila apenas `src/Workoutly.Api`. `render.yaml` é uma alternativa opcional ao cadastro manual; cria somente o Web Service em Oregon. Mude a região se o banco estiver em outra região.

O aplicativo já possui campo de endereço do serviço na tela inicial: informe a URL HTTPS do Render. O limite de espera foi ampliado para 120 segundos para comportar a inicialização do serviço gratuito; uma falha continua preservando os formulários e pode ser repetida pelo usuário.

## Verificar localmente

```sh
dotnet build src/Workoutly.Api/Workoutly.Api.csproj
dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj
```

Sem `WORKOUTLY_TEST_ADMIN_CONNECTION`, os testes PostgreSQL são explicitamente ignorados. Para executar todos, use uma instância PostgreSQL LOCAL de testes e uma conexão Npgsql com permissão de criar bancos. Os testes criam bases aleatórias `workoutly_test_*` e removem somente essas bases. Nunca use as credenciais do banco Render nesses testes.

Para preparar um banco local vazio: configure a conexão da API e execute `dotnet run --project src/Workoutly.Api -- --migrate`. No banco existente versão 9, esse comando não reescreve os dados. Esquemas parciais são recusados; consulte o guia.

## Documentação preservada

Os documentos anteriores em `docs/` descrevem a implementação MySQL histórica. README e scripts locais antigos estão em `docs/legado-mysql/` apenas como referência; não são o caminho de execução desta versão. Os documentos acadêmicos não foram reescritos. A descrição atual da adaptação e os limites de validação estão em `INICIAR-RENDER.md` e `docs/evidencias/postgresql-render.md`.
