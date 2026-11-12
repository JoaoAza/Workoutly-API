# Evidências — registro de treinos

Data: 21/10/2026. Incremento TREINO-01; versão mobile 0.8.0 (8), branch desenvolvimento/registro-treinos, marco Git v0.8.0-treinos. Implementação nova, sem edição dos documentos acadêmicos originais. Os quatro hashes conferidos na pasta superior permanecem iguais aos registrados na etapa anterior.

## Ambiente e compilação

Mac Apple Silicon, SDK .NET 10.0.401, workload 10.0.401.1, MAUI 10.0.110, MySQL nativo 9.6.0, esquema 6. Execução no iPhone 18 Pro Simulator / iOS 27.0 / Xcode 27. Compilações finais iOS (iossimulator-arm64) e Android (JDK 21 explícito): zero avisos e zero erros. API compilada pelo runner de integração.

## Testes automatizados observados

`python3 scripts/test-native-mysql.py`: **24 aprovados, zero falhas, zero ignorados**, usando banco real temporário e isolado. O primeiro teste novo falhou com 404 na consulta /sessions antes da implementação. A instância de teste foi encerrada e removida; não usou nem limpou o banco de demonstração.

Os três testes de WorkoutSessionTests verificaram:

- Registro de séries realizadas diferentes das metas, cargas decimais, duração e observações. O treino iniciado na revisão 1 foi salvo e recuperado mesmo após editar a ficha para revisão 2 e excluí-la. Outra instância da API recuperou o mesmo registro e seus valores originais.
- Propriedade obtida da sessão de acesso, rejeição de ficha alheia e detalhe alheio, listagem isolada por aluno, campos extras incapazes de alterar proprietário/descrição, 0 kg aceito, professor bloqueado nas rotas pessoais de sessões e chamadas anônimas recusadas.
- Entradas inválidas, exercício fora da revisão, ausência de gravação parcial, reenvios simultâneos sem duplicação, equivalência de fuso/ordem de exercícios/casas decimais e 409 para outro conteúdo com o mesmo ID, sem sobrescrever o treino salvo.

O runner também verificou Kestrel real: 503 sem esquema → migração → 200 pronto → reinício da API → 200 pronto. O ambiente local de demonstração retornou `{"status":"ready","database":"ready","schemaVersion":6}`.

## Execução observada no iPhone Simulator

Dados fictícios, na conta de aluno de demonstração já existente; não correspondem a treino real nem à avaliação com participantes.

1. Instalado e aberto o novo build. A sessão de aluno foi restaurada; Minhas fichas mostrou Iniciar treino.
2. Iniciar treino abriu a ficha na revisão 2, com oito séries previstas vazias, metas como referência e cronômetro funcionando.
3. Concluir sem séries marcadas mostrou validação e não enviou registro.
4. Informados Tríceps na polia: 10 repetições × 22,5 kg e 8 × 25 kg; Supino reto: 12 × 40 kg. Marcadas três séries como Feita; as outras cinco permaneceram vazias.
5. Preenchidas observações: “Treino demonstrativo: três séries realizadas.”
6. Interrompido somente o helper local com SIGINT. Concluir e salvar pediu confirmação. Após confirmar, a tentativa sem serviço mostrou erro e Tentar salvar novamente, mantendo séries, cargas, observações e duração fixa de **00:02:45**. Captura ios-treino-sem-conexao.jpg.
7. Reiniciado Executar-local.command; a mesma página reenviou os dados e abriu o detalhe salvo. Foram apresentados somente os dois exercícios e três séries realizados, com a duração preservada e observações corretas. Captura ios-treino-salvo.jpg.
8. Encerrado o app, instalado o build final e reaberto. Treinos salvos apresentou **um registro**, com dois exercícios, três séries e 00:02:45. Ver treino recuperou os mesmos valores e observações. Captura ios-treinos-recuperados.jpg.

A inspeção visual identificou que o aviso de falha podia ficar abaixo da área visível quando o painel de nova tentativa surgia. O código final também solicita rolagem para a ação de salvar após atualizar esse painel; a captura de erro foi obtida com rolagem manual, e a falha de rede completa não foi repetida após esse pequeno ajuste. O fluxo salvo e a recuperação foram revalidados no build final.

## Limites

Android foi compilado, sem execução neste incremento. Windows, aparelho físico, Release e avaliação com três participantes não executados. Reenvios concorrentes e preservação após alterar/excluir ficha foram verificados por HTTP com MySQL, sem simulação manual entre dois aparelhos. Os botões de adicionar/remover série, a confirmação de saída e o acesso à lista durante envio pendente não foram percorridos manualmente nesta rodada.

Os formulários permanecem somente na página aberta; não há rascunho persistente nem sincronização offline. O teste manual simulou serviço indisponível antes do envio; a perda da resposta após um commit foi coberta pela repetição do mesmo POST no teste HTTP, não por interrupção manual de pacotes.

Correção de sessão, indicadores e acompanhamento pelo professor continuam pendentes. A lista e o detalhe próprios servem para recuperar e conferir o registro; não concluem HIST-01.
