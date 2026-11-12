# Evidências — fichas de treino

Data: 21/10/2026. Incremento FICHA-01, versão mobile 0.7.0 (7), branch desenvolvimento/fichas, marco Git v0.7.0-fichas. Documentos acadêmicos originais da pasta superior preservados; os quatro hashes conferidos continuam iguais aos registrados nos incrementos anteriores.

## Ambiente

Mac Apple Silicon, SDK .NET 10.0.401, workload 10.0.401.1, MAUI 10.0.110, MySQL nativo 9.6.0, esquema 5. iPhone 18 Pro Simulator / iOS 27.0 / Xcode 27. Android compilado usando JDK 21 instalado neste Mac.

## Verificação automatizada observada

`python3 scripts/test-native-mysql.py`: **21 aprovados, zero falhas, zero ignorados**. Banco real temporário, separado do banco de demonstração; encerrado e removido pelo runner. O primeiro teste novo falhou com 404 antes da existência da rota. A primeira migração nova revelou que sets é nome reservado no MySQL; a coluna foi corrigida para set_count antes de usar o ambiente persistente.

Os três testes de PlanTests cobrem:

- CRUD do aluno, ordem e valores de séries/repetições, revisão anterior preservada, exclusão lógica, isolamento entre alunos, autenticação, persistência após outra instância da API e conflito ao excluir/editar versão antiga.
- Professor cria ficha para aluno vinculado; aluno recebe os dados de exercício privado incluído sem acesso ao catálogo privado. Tentativa de adulterar proprietário/nome de exercício não altera os dados determinados pelo servidor. Outro professor não lista, consulta, cria ou altera; desvincular bloqueia imediatamente as operações do professor anterior e mantém a ficha do aluno. Novo professor só acessa após vínculo aceito.
- Campos inválidos, exercício inexistente/privado alheio, gravação atômica, duas criações simultâneas com o mesmo ID sem duplicação e duas edições concorrentes com uma resposta 200 e outra 409.

O runner também confirmou Kestrel por TCP: prontidão 503 sem esquema → comando de migração → 200 pronto → reinício da API → 200 pronto. A migração reaplicada mantém o estado persistido. A prontidão do ambiente de demonstração retornou schemaVersion 5.

Compilações finais de Workoutly.Mobile: iOS net10.0-ios / iossimulator-arm64 e Android net10.0-android, ambas com **zero avisos e zero erros**. O código da API também foi compilado pelo runner.

## Execução observada no iPhone Simulator

Usadas somente as contas fictícias locais de demonstração já existentes.

1. A sessão anterior de professor foi restaurada após instalar a versão nova.
2. Montar fichas → aluno vinculado → lista vazia → Nova ficha.
3. Salvar sem nome mostrou validação; nenhum registro criado.
4. Preenchidos nome e observações; busca por Supino e por triceps encontrou os movimentos, incluindo comparação sem acento.
5. Incluídos Supino reto e Tríceps na polia; supino ajustado para 4 × 12. Subir alterou a ordem para tríceps antes do supino, preservando os campos.
6. Ambiente local encerrado com SIGINT somente no helper do projeto. Salvar mostrou falha de conexão e manteve nome, observações, dois exercícios, ordem, séries e repetições. Captura ios-ficha-sem-conexao.jpg.
7. Ambiente reiniciado; a mesma página salvou a ficha. Lista do professor apresentou 2 exercícios e 7 séries previstas. Captura ios-fichas-professor.jpg.
8. Logout de professor → login de aluno → Minhas fichas: a mesma ficha apareceu. O aluno alterou o nome para Treino A — Peito e tríceps e o tríceps para 4 séries; salvou a revisão, totalizando 8 séries previstas.
9. Aplicativo encerrado e reaberto, com instalação do build final. Sessão de aluno restaurada; ficha recuperada com 2 exercícios e 8 séries, na ordem salva. Reaberta com observações, tríceps 4 × 10 e supino 4 × 12 preservados. Captura ios-fichas-aluno.jpg.
10. Excluir ficha mostrou confirmação; Cancelar manteve a ficha de demonstração. Exclusão efetiva foi verificada pelos testes HTTP, não executada nessa ficha no simulador.

## Limites

Android: compilação, sem execução neste incremento. Windows, aparelho físico, Release e avaliação com participantes não executados. Não foi realizado ensaio manual de conflito concorrente entre dois aparelhos; esse caso passou no teste HTTP com MySQL. O cadastro de exercício a partir do seletor e a confirmação de recarregamento não foram percorridos manualmente nesta rodada. O formulário conserva dados durante a página aberta, sem rascunho persistente após encerrar o app.

Sessões, histórico e indicadores de treino continuam pendentes. Revisões e exclusão lógica preparam sua preservação, mas não constituem um histórico de sessões implementado. Os resultados acima não substituem a avaliação acadêmica com três participantes reais.
