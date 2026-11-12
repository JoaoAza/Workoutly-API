# Workoutly

Aplicativo acadêmico de acompanhamento de treinos, criado do zero para o Projeto Integrador 6.

## Situação atual

O [balanço de aderência aos roteiros e à Fase 2](docs/situacao-atual-e-aderencia-fase-2.md) consolida o que já foi implementado, as evidências disponíveis e as pendências técnicas e acadêmicas da versão 0.11.0. Inclui a comparação por seção do relatório e um plano de fechamento sem modificar as entregas originais.

O app já permite **cadastro, login, recuperação de sessão, logout e catálogo de exercícios**, integrado à API e ao MySQL. Há 30 exercícios compartilhados ilustrados e cadastro de exercícios privados da conta, com busca e detalhes. A sessão fica no armazenamento seguro do aparelho e é revogada no servidor ao sair. Fichas já permitem criar, consultar, editar, ordenar exercícios e excluir com revisões preservadas. O aluno também registra séries realizadas, cargas, duração e observações e recupera seus treinos salvos. Correção dos registros próprios já está disponível em Histórico e evolução → Ver treino → Corrigir treino, com totais recalculados. Histórico por período, frequência, volume e evolução de carga estão disponíveis; o professor consulta somente alunos vinculados. A experiência de uso já inclui confirmação ao sair de formulários alterados, recuperação de acesso e distinção entre falha de carregamento e listas vazias. A validação multiplataforma e a avaliação final do MVP permanecem pendentes.

Nenhum código do aplicativo anterior foi reutilizado. Os roteiros enviados ao professor permanecem na pasta superior, sem alterações.

A interface segue a identidade visual do Figma do grupo: fundo escuro, verde-lima, detalhes turquesa, fonte Inter, marca e cartões arredondados. A aplicação às telas atuais e as referências estão em [Identidade visual](docs/identidade-visual.md).

Há dois perfis: **aluno** e **professor**. A primeira tela permite escolher como acessar; em seguida é possível entrar ou criar uma conta daquele perfil. A API aceita somente `student` e `teacher`, grava essa escolha no MySQL e usa o papel persistido para autorizar cada requisição — selecionar professor não concede acesso global a contas ou dados. O professor gera um código em Meus alunos; o aluno informa esse código em Meu professor e confirma o vínculo. Detalhes em [Perfis e vínculos](docs/perfis-e-vinculos.md). O aluno acessa Minhas fichas; professor usa Montar fichas → Fichas do aluno. Veja [Fichas](docs/fichas.md). Aluno inicia pela ficha e recupera em Histórico e evolução; veja [Registro de treinos](docs/treinos.md). Professor acompanha em Meus alunos → Histórico do aluno, somente de leitura. Veja [Histórico e armazenamento](docs/historico-e-armazenamento.md).

As descrições dos exercícios incluem os músculos trabalhados e os papéis de auxiliares e estabilizadores. A [revisão muscular e suas fontes](docs/revisao-musculos-exercicios.md) registra as variantes consideradas e os limites de identificação dos exemplos genéricos.

## Componentes

| Local | Responsabilidade |
| --- | --- |
| `src/Workoutly.Mobile` | Interface mobile nativa em C# e XAML para Android e iOS. |
| `src/Workoutly.Api` | Serviço HTTP ASP.NET Core. MySQL, autenticação por sessão revogável, autorização da conta e prontidão. |
| `tests/Workoutly.Api.Tests` | Testes HTTP e teste MySQL real em base aleatória. Testes de dispositivo continuam separados. |
| `docs` | Requisitos, backlog, decisões e evidências. |
| `.agents/skills` | Procedimentos de apoio ao Codex, não dependências do app. |

## Pré-requisitos

- SDK .NET **10.0.401** (ou patch compatível), workload set **10.0.401.1** e MAUI **10.0.110**; ver `global.json` e `Directory.Build.props`.
- Workloads MAUI: `dotnet workload restore src/Workoutly.Mobile/Workoutly.Mobile.csproj`.
- Mac para compilar e executar iOS: Xcode selecionado, runtime do simulador e componentes compatíveis com o workload.
- Android: Android SDK e JDK 21. O teste no Windows requer a configuração desses componentes nesse computador.
- Para instalar em iPhone físico: assinatura e provisionamento Apple.

Neste Mac o SDK utilizado está em `/Users/pedrobaldi/.dotnet/dotnet`. Se o comando `dotnet --version` mostrar outro SDK, use esse executável ou ajuste o PATH do terminal/IDE.

## Testar cadastro e login neste Mac

Dê dois cliques em `Executar-local.command`, ou execute:

```sh
python3 scripts/dev-native.py
```

Esse helper usa o MySQL nativo instalado, em uma instância exclusiva do projeto, e inicia a API em localhost. Os dados ficam em `.local/`, ignorado pelo Git, e sobrevivem ao reinício. Use **Ctrl+C** no terminal para encerrar os serviços sem apagar as contas. O Docker não é necessário nesse caminho. Não execute uma segunda instância enquanto a primeira estiver aberta.

Em seguida, execute o projeto MAUI no simulador pela IDE, toque em **Criar uma conta** e cadastre dados fictícios de teste. Na conta, **Explorar exercícios** abre o catálogo; **Novo exercício** cria um registro privado. Os contratos e explicações estão em [Conta e acesso](docs/autenticacao.md) e [Catálogo de exercícios](docs/exercicios.md).

## Abrir e executar

Abra `Workoutly.slnx` no Rider ou Visual Studio compatível com .NET 10.

### API e testes

Na raiz do repositório:

```sh
dotnet run --project src/Workoutly.Api --launch-profile http
```

Abra http://localhost:5080/health. Resposta esperada:

```json
{"service":"Workoutly.Api","status":"ok","databaseConfigured":false}
```

Sem conexão configurada, `/health` continua respondendo 200 com `databaseConfigured=false`, e `/health/ready` retorna 503. Para preparar o MySQL e executar os testes reais, leia [MySQL no Workoutly](docs/mysql.md).

A API de desenvolvimento escuta somente no computador local. O simulador de iPhone usa http://localhost:5080/ e o emulador Android usa http://10.0.2.2:5080/. Em builds Release, o app exige um endereço HTTPS. Telefones físicos precisam de um serviço HTTPS acessível; não usam o localhost do Mac.

```sh
dotnet build src/Workoutly.Api/Workoutly.Api.csproj
dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj
```

### iPhone no Mac

Na IDE, selecione `Workoutly.Mobile`, o framework `net10.0-ios`, um simulador de iPhone e **Run**.

Para conferir apenas a compilação no Mac Apple Silicon:

```sh
dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
```

### Android no Mac ou Windows

Na IDE, selecione `Workoutly.Mobile`, `net10.0-android` e um emulador ou aparelho Android com depuração USB.

```sh
dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android
```

Neste Mac, o SDK detectou Java 25 por padrão. Para usar o JDK 21 já instalado sem alterar o Java global:

```sh
dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```

Esse caminho é específico deste Mac. No Windows ou em outro Mac, selecione o JDK 21 instalado na máquina pela IDE ou pela propriedade `JavaSdkDirectory`.

A solução inclui o projeto mobile: compilar a solução inteira exige os workloads das plataformas. Para trabalhar somente no backend, compile o projeto da API e execute seu projeto de testes.

## Como vamos desenvolver

Leia `docs/backlog.md` para a sequência e critérios de aceite, `docs/arquitetura.md` para a separação de responsabilidades e `docs/skills.md` para usar as skills instaladas. A persistência MySQL foi implementada na API. Prepare e verifique seu ambiente seguindo `docs/mysql.md`. Conta, acesso, exercícios, vínculos e fichas estão implementados. Registro e consulta de sessões próprias também estão implementados. Correção dos registros próprios também está implementada; veja [Correção de treinos](docs/correcao-treinos.md). Histórico, indicadores e acompanhamento pelo professor também estão implementados; veja [Histórico e armazenamento](docs/historico-e-armazenamento.md). UX-01 está concluído; veja [Experiência de uso](docs/experiencia-de-uso.md). O próximo incremento é QA-01: execução do fluxo completo nas plataformas e registro dos resultados.

Ao concluir cada incremento serão registrados o conceito, a decisão, onde aparece no código e a forma de testar. Slides e avaliação com participantes permanecem para as etapas correspondentes.

O openGym é referência de experiência e organização, não código incorporado. Ver `docs/referencias.md`.
