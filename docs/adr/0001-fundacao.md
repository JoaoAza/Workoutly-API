# Decisão 0001 Fundação nova em .NET

Data: 20/10/2026. Estado: adotada para esta etapa.

## Contexto

O usuário escolheu reconstruir do zero, com os roteiros acadêmicos como fonte de requisitos e o openGym como referência. O aplicativo antigo não será reutilizado.

## Decisão

Usar .NET 10, aplicativo MAUI em C# e XAML para Android/iOS, API ASP.NET Core e testes xUnit. Fixar SDK e MAUI disponíveis neste Mac. Começar com tela inicial e endpoint de saúde; MySQL é o próximo incremento. Não adicionar pacotes de persistência, autenticação, gráficos ou MVVM antes de haver necessidade implementada.

O Git pertence à nova pasta Workoutly; os documentos originais permanecem na pasta superior. O ramo inicial é main. Não há remote nem publicação. Tarefas são mantidas localmente em docs/backlog.md. As skills são cópias versionadas e atualizações exigem revisão explícita.

## Consequências

A fundação compila independentemente de MySQL. O app ainda não conversa com a API e não atende ao fluxo principal do MVP. Não migrar dados nem incorporar código do openGym. Explicar e testar cada incremento antes de avançar.
