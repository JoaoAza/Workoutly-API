# Publicar o Workoutly no Render

## O que já está pronto

Este pacote adapta a API para PostgreSQL/Npgsql e contém Dockerfile, configuração de porta, encaminhamento HTTPS, testes e aplicativo MAUI. Ele não foi publicado na sua conta. Seu banco já criado no Render, com 30 exercícios e esquema 9, deve ser reutilizado. **Não apague nem recrie o banco; não execute novamente 01_estrutura.sql.**

## 1. Enviar o projeto ao GitHub

A opção mais fácil para este projeto com várias pastas é GitHub Desktop:

1. Descompacte o ZIP. Abra a pasta `Workoutly-Render`: ela contém `Dockerfile`, `src`, `global.json` e este guia.
2. No GitHub Desktop, use File → Add local repository e escolha essa pasta. Se ainda não for um repositório Git, use a opção para criar um repositório nesse local.
3. Confira que a pasta raiz do repositório contém o `Dockerfile`, e não outra pasta `Workoutly-Render` acima dele.
4. Faça um commit com os arquivos e clique em Publish repository. Pode manter o repositório privado.
5. Não adicione senha, URL de conexão do banco, arquivos `.env`, `bin` ou `obj` ao repositório. `.gitignore` já exclui arquivos locais usuais.

Se já tiver um repositório do projeto, copie esta versão sobre uma branch separada e remova os antigos `Data/MySqlDatabase.cs`, arquivos SQL MySQL em `src/Workoutly.Api/Data/Migrations/` e `tests/Workoutly.Api.Tests/MySqlBootstrapTests.cs`. O mais simples para uma primeira publicação é usar um repositório novo com o conteúdo integral deste pacote.

## 2. Criar o serviço da API no Render

No Render, escolha **New → Web Service** e conecte o repositório. Configure:

| Campo | Valor |
| --- | --- |
| Name | workoutly-api (ou outro nome disponível) |
| Region | Oregon, se for a região do banco mostrado anteriormente; confira no painel |
| Language/Runtime | Docker |
| Branch | A branch em que você enviou esta versão |
| Root Directory | Vazio, se Dockerfile está na raiz do repositório |
| Dockerfile Path | ./Dockerfile |
| Docker Build Context Directory | . |
| Docker Command | Vazio; o Dockerfile já define o comando |
| Instance Type | Free para testes temporários, ou plano pago conforme necessidade |
| Health Check Path | /health/ready |

Não selecione Static Site. Não é necessário instalar Docker no seu computador para o Render construir a imagem a partir do repositório. O aplicativo MAUI não é compilado pelo Dockerfile.

## 3. Configurar Environment Variables

Antes do deploy, adicione no serviço da API:

| Key | Value |
| --- | --- |
| ASPNETCORE_ENVIRONMENT | Production |
| Hosting__RenderProxy | true |
| DATABASE_URL | Cole a Internal Database URL do banco existente |

Copie **Internal Database URL** pelo botão Connect do banco Render. Use somente no campo secreto DATABASE_URL do serviço. Não cole a URL em arquivos, prints ou no campo de endereço do aplicativo.

O banco e a API devem estar na mesma conta e região para essa conexão interna. Se estiverem em regiões diferentes, corrija a região da API antes de criá-la.

A API aceita URLs `postgresql://usuario:senha@host/banco` e converte para conexão Npgsql, exigindo TLS. Alternativamente aceita `ConnectionStrings__Workoutly` em formato `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require`. **Escolha só uma configuração**: se ambas estiverem preenchidas, ConnectionStrings__Workoutly tem prioridade. Não use a URL de conexão MySQL antiga.

`Hosting__RenderProxy=true` permite reconhecer os cabeçalhos encaminhados pelo proxy do Render antes da autenticação/limitação de requisições. Use essa opção apenas nesse ambiente protegido pelo proxy, não em um servidor exposto diretamente. `PORT` é respeitada pelo processo; o padrão da imagem é 10000.

## 4. Publicar e conferir

Clique em Deploy Web Service. Aguarde a conclusão e abra a URL HTTPS mostrada no painel. Exemplos ilustrativos (substitua pelo seu domínio real):

- `https://SEU-SERVICO.onrender.com/health`: deve mostrar status `ok` e databaseConfigured `true`.
- `https://SEU-SERVICO.onrender.com/health/ready`: deve mostrar status `ready`, database `ready` e schemaVersion `9`.

A raiz `/` pode responder 404: isso não é falha; esta API não possui página inicial nem Swagger. Use os endpoints acima.

**O deploy não executa migrações nem insere novamente o catálogo.** O banco que você já preparou deve atender ao health check. Não configure Pre-Deploy Command para recriar tabelas.

## 5. Conectar o app

Abra a solução `Workoutly.slnx` em uma IDE compatível, compile/execute `Workoutly.Mobile` no emulador ou celular. Na tela de acesso, abra a opção **Conexão** para mostrar **Endereço do serviço** e informe somente:

`https://SEU-SERVICO.onrender.com/`

Não acrescente `/health`, `/auth`, usuário/senha do banco nem a URL PostgreSQL. Selecione o perfil e crie uma conta de teste pelo próprio aplicativo. O aplicativo guarda a URL selecionada e continua usando o armazenamento seguro para o token. A senha do PostgreSQL nunca vai para o celular.

O timeout do cliente foi ampliado de 10 para 120 segundos para o primeiro acesso ao plano Free. Caso ocorra falha, os formulários continuam disponíveis para nova tentativa; não foram criadas retentativas automáticas que dupliquem cadastro ou treino.

Confira cadastro/login, catálogo, criação de ficha e registro de treino. Reinicie o aplicativo e confirme a recuperação. Para professor, cadastre outra conta, gere o convite, vincule o aluno e confira o histórico.

## Se algo falhar

| Sintoma | Verificação |
| --- | --- |
| Build não encontra Dockerfile | Repositório/pasta raiz incorretos; Dockerfile deve estar na raiz configurada |
| Build tenta compilar MAUI | Use este Dockerfile, não `dotnet build` da solução inteira |
| /health mostra databaseConfigured false | DATABASE_URL ausente ou vazia |
| /health/ready mostra invalid_configuration | URL incompleta ou string de conexão em formato incorreto |
| /health/ready mostra unreachable | Credenciais, estado do banco, região, endereço interno/externo e regras de rede |
| /health/ready mostra schema_pending | Banco errado ou scripts 01 e 02 ainda não concluídos; não apague o banco |
| Login responde “Use HTTPS” com URL HTTPS | Confira Hosting__RenderProxy=true e faça novo deploy |
| Primeiro acesso demora | Plano Free pode estar retomando após inatividade |
| / responde 404 | Use /health ou /health/ready; não há página inicial |

Ao pedir ajuda, envie o erro de build ou o JSON do health check, ocultando segredos. Os endpoints de saúde não devolvem a conexão nem a senha.

## Limitações dos planos e validação

O serviço Free entra em repouso após 15 minutos sem tráfego e pode levar cerca de um minuto para retomar. O banco Free expira após 30 dias. Escolha um plano adequado ao prazo do trabalho. API e banco são serviços separados: hospedar um não hospeda o outro.

Os testes executados nesta entrega e o que ainda depende da sua conta/dispositivo estão em `docs/evidencias/postgresql-render.md`. Um teste local não comprova publicação no Render nem execução no Android/iOS.

Referências consultadas: https://render.com/docs/docker, https://render.com/docs/web-services, https://render.com/docs/postgresql-creating-connecting, https://render.com/docs/free e https://www.npgsql.org/doc/.

## Desenvolvimento local opcional

`compose.yaml` fornece PostgreSQL 18 local. Defina `WORKOUTLY_POSTGRES_PASSWORD` no seu terminal e execute `docker compose up -d`. Configure `ConnectionStrings__Workoutly` com host localhost, banco/usuário workoutly e a senha escolhida. Para preparar essa base LOCAL vazia:

```sh
dotnet run --project src/Workoutly.Api -- --migrate
dotnet run --project src/Workoutly.Api --launch-profile http
```

Para testes reais, configure `WORKOUTLY_TEST_ADMIN_CONNECTION` com uma conexão PostgreSQL local com permissão de criar bases, e execute:

```sh
dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj
```

Não use seu banco do Render nos testes. Sem essa variável, os testes que exigem banco são explicitamente ignorados. O bootstrap PostgreSQL é transacional e protegido contra duas execuções simultâneas. Uma base existente com versão 9 não é alterada; bases incompletas são recusadas em vez de serem reparadas silenciosamente.
