# Verificação da fundação

Data: 20/10/2026. Marco Git: `v0.1.0-base` (identifica o commit desta entrega).

## Ambiente

Mac Apple Silicon, SDK .NET 10.0.401, workload set 10.0.401.1, MAUI 10.0.110. iPhone 18 Pro no simulador iOS 27.0, bundle `br.edu.workoutly.mobile`. Compilação MAUI com dotnet; instalação, lançamento e captura por XcodeBuildMCP.

## Resultados

| Verificação | Resultado |
| --- | --- |
| API e testes | Compilados; 1 teste HTTP aprovado via WebApplicationFactory, runtime .NET 10.0.12. |
| iOS | Build Debug net10.0-ios / iossimulator-arm64 aprovado, zero avisos e erros. |
| Execução iOS | Aplicativo instalado e iniciado; captura e árvore de interface confirmam a tela inicial. |
| Android | Build Debug net10.0-android aprovado com JDK 21, zero avisos e erros. |
| Skills | 11 SKILL.md presentes; 320 arquivos conferidos por SHA-256 com docs/skills.lock.json. |
| Documentos originais | Quatro arquivos conferidos por SHA-256 antes/depois; nenhuma alteração. |

Comandos da raiz (neste Mac foi utilizado `/Users/pedrobaldi/.dotnet/dotnet`):

```sh
dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj --logger 'console;verbosity=normal'
dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```

O primeiro build Android selecionou JDK 25 e falhou (XA0030). A seleção explícita do JDK 21 e a restauração dos ativos Android resolveram a compilação, sem alteração do Java padrão da máquina.

## Evidência visual

![Tela inicial do novo Workoutly no simulador de iPhone](ios-fundacao.jpg)

A tela é a fundação visual; os textos sobre fichas, sessões e evolução descrevem a proposta do produto. Não existem ainda ações ou fluxos dessas funcionalidades.

## Integridade dos documentos originais

Hashes antes/depois idênticos:

| Arquivo na pasta superior | SHA-256 |
| --- | --- |
| Roteiro Fase 2 - Workoutly.docx | 8e524ff1c2fe35742b20637aba8dc099a09667572b2fa0ba4a45a0cffc959537 |
| Roteiro Workoutly.docx | 5f967b3447dfb1ab7344226b3014fbfee8fceaa6943982401587d777c9c9c187 |
| Roteiro Fase 2 - Workoutly.docx.md | 9a1282b7b3706633d914ded918c9df9f444b0066e3b518be5fe8d6c6c9c76e43 |
| Roteiro Workoutly.md | fffda0746178328c4e15fee0e844c5897a4fedac94be31cc93b028435a612614 |

## Limites

Não foram executados fluxos de cadastro, login, fichas ou sessões: não estão implementados. O endpoint de saúde não comprova banco ou segurança. MySQL, API consumida pelo aplicativo e testes com três participantes são próximos incrementos. Não foram testados Windows nem aparelhos físicos. Execução Android não foi realizada; compilação e execução são verificações diferentes.
