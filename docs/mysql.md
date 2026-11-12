# MySQL no Workoutly

## O que este incremento entrega

A API conecta ao MySQL com MySqlConnector 2.6.2. A preparação explícita aplica as versões 1 (bootstrap), 2 (contas/sessões de acesso), 3 (exercícios), 4 (perfis/vínculos), 5 (fichas/revisões/itens), 6 (sessões/séries realizadas) e 7 (identificação das correções) em `schema_migrations`. Essa tabela registra o esquema preparado. Sessões e séries realizadas referenciam a revisão executada; detalhes em treinos.md.

O aplicativo MAUI já chama a API para conta e acesso; não conhece credenciais MySQL.

## Ambiente local reproduzível

Pré-requisitos: SDK .NET definido em global.json, Docker Desktop iniciado e Python 3. No Windows, use `python` em lugar de `python3` se necessário. Execute a partir da raiz do repositório:

```sh
python3 scripts/dev.py init
docker compose up -d --wait mysql
python3 scripts/dev.py migrate
python3 scripts/dev.py test-db
python3 scripts/dev.py run
```

O comando `init` cria `.env` com senhas aleatórias e se recusa a substituir um arquivo existente. O Python lê esse arquivo como dados: não execute `source .env`. As credenciais não são impressas. O arquivo está ignorado pelo Git; `.env.example` documenta as variáveis sem conter credenciais reais.

O Compose utiliza `mysql:9.6.0`, porta 3307 apenas em `127.0.0.1` e volume `mysql_data`. Essa porta evita ocupar o padrão 3306 de outras instalações. Para alterar a porta, ajuste **MYSQL_PORT e ConnectionStrings__Workoutly** no `.env`.

A API usa a conta `workoutly`, limitada ao banco `workoutly`. A conta administrativa é usada apenas para criar e remover bancos aleatórios nos testes; o script não a envia como conexão da API. O diretório de dados não faz parte do repositório.

Para parar preservando os dados:

```sh
docker compose stop mysql
```

Para retomar, execute `docker compose up -d --wait mysql`. Não remova o volume para reiniciar e não regenere senhas de um volume existente: variáveis de inicialização não redefinem usuários já criados.

## Teste real sem Docker neste Mac

Há uma alternativa para validar a integração com o MySQL nativo instalado, sem usar o servidor nem os dados existentes:

```sh
python3 scripts/test-native-mysql.py
```

O script cria um diretório temporário e inicia `mysqld --no-defaults --skip-networking`, acessível somente por socket Unix. Não usa arquivos de configuração do usuário e não abre uma porta TCP. Executa a suíte de testes e uma verificação HTTP com Kestrel, aplica a migração e verifica nova leitura depois de reiniciar a API. Ao terminar, encerra os processos e remove o diretório que criou. Esse caminho é para macOS/Linux com `mysqld` e `mysql` no PATH; não deixa um servidor de desenvolvimento permanente rodando.

## Configuração sem o helper

`ConnectionStrings__Workoutly` é uma variável de ambiente lida pelo ASP.NET Core como `ConnectionStrings:Workoutly`. Defina-a no ambiente do processo da API com os valores do servidor escolhido. O formato é:

```text
Server=HOST;Port=PORTA;Database=BANCO;User ID=USUARIO;Password=SENHA
```

O `.env` não é carregado automaticamente por `dotnet run`: use o helper ou configure a variável explicitamente. O helper preserva uma conexão já definida no ambiente em vez de sobrescrevê-la. `SslMode=Required` exige TLS no ambiente local; para servidor remoto, configure TLS e validação de certificado conforme a infraestrutura, usando VerifyFull e os certificados apropriados.

```sh
dotnet run --project src/Workoutly.Api --no-launch-profile -- --migrate
dotnet run --project src/Workoutly.Api --no-launch-profile --urls http://localhost:5080
```

O banco e o usuário devem existir antes da migração; no caminho local o Compose os cria. A API não cria usuários MySQL, não requer root e não modifica o esquema ao iniciar o servidor HTTP. O comando de migração é reaplicável e termina com código diferente de zero quando há falha, sem imprimir a conexão.

## Contratos HTTP

| Rota | Significado | Resposta |
| --- | --- | --- |
| GET /health | Processo da API atende HTTP, mesmo sem banco. | 200; `databaseConfigured` indica somente a presença da configuração. |
| GET /health/ready | Conexão ao banco escolhido e leitura da versão do esquema. | 200 se versão 7; 503 se não pronto. |

Exemplo de prontidão:

```json
{"status":"ready","database":"ready","schemaVersion":9}
```

Estados sem prontidão: `not_configured`, `invalid_configuration`, `unreachable` ou `schema_pending`. A sondagem tem limite de 3 segundos. A resposta não revela conexão, usuário, senha ou detalhes do driver. Ela não executa DDL e não comprova requisitos futuros de autenticação ou treino.

## Testes e persistência

`dotnet test tests/Workoutly.Api.Tests` executa os testes HTTP sem servidor MySQL. O teste marcado `Category=MySql` é explicitamente ignorado se o ambiente de banco não foi fornecido; isso não conta como validação real de persistência.

`python3 scripts/dev.py test-db` fornece o servidor de testes local e executa todos os testes, incluindo o real. A fixture cria uma base `workoutly_test_<identificador aleatório>`, verifica a indisponibilidade antes da preparação, aplica a migração duas vezes e inicia outra instância da API para verificar que a versão foi persistida. Ao terminar, remove somente a base aleatória que criou. Não apaga o banco da aplicação.

Para uma instância de testes externa, defina `WORKOUTLY_TEST_ADMIN_CONNECTION` de um servidor isolado; a conta precisa de CREATE/DROP DATABASE. O helper não substitui esse valor. Não usar uma instância com dados reais para testes administrativos.

## Como os componentes se relacionam

- `MySqlDatabase` abre conexões e executa os comandos SQL. `await using` fecha as conexões corretamente, inclusive em falhas.
- A migração é um recurso embutido no assembly da API: continua disponível após publicação, independentemente da pasta de execução.
- `/health/ready` testa uma leitura real da tabela; o registro da versão persiste no volume MySQL entre execuções da API.
- `scripts/dev.py` prepara o ambiente e chama dotnet sem passar senhas pela linha de comando.
- Docker fornece o mesmo tipo de servidor no Mac e Windows; o driver C# fica apenas na API.

Referências técnicas: [API ADO.NET do MySqlConnector](https://mysqlconnector.net/tutorials/basic-api/) e [opções de conexão](https://mysqlconnector.net/connection-options/).
