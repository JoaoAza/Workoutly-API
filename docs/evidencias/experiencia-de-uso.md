# UX-01 — experiência de uso

Data: 21/10/2026. App 0.11.0 (11), branch `desenvolvimento/experiencia-uso`, tag `v0.11.0-experiencia-uso`. Base anterior: `v0.10.0-historico`. Alterações somente no aplicativo e na documentação; API e esquema MySQL 7 preservados.

## Ambiente e verificações

Mac Apple Silicon, .NET SDK 10.0.401, workload set 10.0.401.1, MAUI 10.0.110. Execução manual no iPhone 18 Pro Simulator, iOS 27.0. API local na porta 5080 e MySQL nativo 9.6.0 persistente para as contas fictícias de demonstração.

Compilações finais executadas:

```sh
/Users/pedrobaldi/.dotnet/dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
/Users/pedrobaldi/.dotnet/dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```

Ambas: **0 avisos, 0 erros**. A versão final foi instalada e aberta novamente no simulador.

`python3 scripts/test-native-mysql.py`: **30 testes aprovados, 0 falhas, 0 ignorados**. A suíte existente usou MySQL temporário e isolado, confirmou falta de esquema com HTTP 503, migração explícita, prontidão HTTP 200 e persistência após reiniciar a API. Não foram adicionados testes de métodos privados de interface; os ajustes de UX foram verificados pelo aplicativo em execução. Testes automatizados não comprovam os resultados visuais abaixo.

## Resultados observados na interface

| Procedimento | Resultado observado |
| --- | --- |
| Professor → Montar fichas → Fichas do aluno | Recuperou a ficha original do aluno vinculado; ação Iniciar treino não aparece para o professor. |
| Nova ficha, preencher nome e tocar em voltar | Diálogo Descartar alterações da ficha? com opções de descarte e continuidade. |
| Escolher Continuar editando | Retornou à mesma ficha, com o nome preservado. |
| Salvar sem exercício | Mensagem Adicione pelo menos um exercício à ficha; os campos permaneceram. |
| Adicionar exercício pelo seletor | Retornou à ficha com o nome preservado e Agachamento livre, 3 séries e 10 repetições. |
| Parar somente o servidor do projeto e salvar | Erro de conexão; nome, exercício e metas preservados e formulário liberado para repetir a tentativa. |
| Reiniciar os serviços e salvar a mesma ficha | Salvamento concluído; retorno automático à lista, sem confirmação de descarte, e ficha disponível. |
| Atualizar lista com os serviços parados | Lista antiga oculta, mensagem Lista não carregada e botão Tentar novamente; sem mensagem de ausência de fichas. |
| Abrir o aplicativo com token válido e serviço parado | Erro de conexão e botão Tentar recuperar acesso. |
| Reiniciar o serviço e tocar em Tentar recuperar acesso | Perfil de professor restaurado sem preencher e-mail/senha. |
| Novo exercício vazio → Salvar | Mensagens completas dos três campos obrigatórios; formulário e botão de salvamento disponíveis. |
| Preencher nome do exercício e voltar | Confirmação Descartar novo exercício?; descarte retornou ao catálogo, sem criar exercício. |
| Entrar como aluno e consultar histórico/detalhes | Treino anterior preservado: 3 séries, 28 repetições, 00:03:30, volume 995 kg. |
| Abrir correção e cancelar sem mudanças | Retornou ao detalhe sem diálogo desnecessário. |
| Alterar segundos de 30 para 31 e voltar | Confirmação Descartar correção?; descarte manteve o registro salvo em 00:03:30 e 995 kg. |
| Iniciar treino de teste e voltar | Confirmação Sair deste treino?; saída retornou às fichas, sem salvar nova sessão. |
| Editar ficha com 0 séries e salvar | Validação rejeitou o valor. Na versão final, mensagem completa acima das ações, após rolagem. Restaurar 3 séries e voltar não pediu descarte, pois os campos ficaram iguais aos carregados. |
| Encerrar/reinstalar/abrir a versão final | Sessão de aluno restaurada e ficha de demonstração recuperada. |

Foi criada somente uma ficha fictícia adicional para verificar salvamento após reconexão: **Ficha para conferir navegação**, do aluno de demonstração, com Agachamento livre, 3 séries × 10 repetições. Ela ficou disponível como exemplo local. O teste de treino em andamento foi descartado; não criou sessão. A ficha original e o treino anterior não foram alterados pelos testes desta etapa.

## Capturas

- [Confirmação ao sair da ficha](ios-ux-confirmar-saida.jpg)
- [Campos preservados na falha de conexão](ios-ux-ficha-preservada.jpg)
- [Erro da ficha completo na versão final](ios-ux-erro-ficha-visivel.jpg)
- [Lista sem conexão e nova tentativa](ios-ux-lista-sem-conexao.jpg)
- [Recuperação de acesso disponível](ios-ux-recuperar-acesso.jpg)
- [Validação do exercício](ios-ux-validacao-exercicio.jpg)
- [Confirmação ao descartar correção](ios-ux-confirmar-correcao.jpg)

A captura de preservação da ficha foi feita durante a verificação que revelou texto cortado no rodapé. A posição da mensagem e a rolagem foram corrigidas depois; a captura Erro da ficha completo registra a versão final. As imagens documentam o procedimento, sem substituir os testes em outras plataformas.

## Limites e próxima etapa

- O gesto lateral foi tentado na ficha em edição, sem produzir navegação no ambiente; não foi possível confirmar seu comportamento completo por essa tentativa. O código centraliza retornos canceláveis do Shell, mas gestos e botão físico de voltar do Android precisam da verificação de QA-01.
- Android foi compilado; não foi executado. Windows, iPhone físico, Release, telas pequenas/fontes ampliadas e leitores de tela continuam sem validação nesta etapa.
- Campos preservados ficam em memória da página. Não há rascunho persistente, recuperação após encerramento do app nem sincronização offline.
- Não houve avaliação com participantes reais. QA-01, AVAL-01 e DOC-01 permanecem pendentes.

Conceitos, decisões, arquivos e sequência de repetição estão em [Experiência de uso](../experiencia-de-uso.md). Documentos acadêmicos da pasta superior permanecem inalterados.
