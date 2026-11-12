# Evidências — correção de treinos

Data: 21/10/2026. TREINO-02, app 0.9.0 (9), branch desenvolvimento/correcao-treinos, marco Git v0.9.0-correcao-treinos. Os quatro hashes dos documentos originais na pasta superior continuam iguais aos registrados nas etapas anteriores; nenhum deles foi editado.

## Ambiente e builds

Mac Apple Silicon, .NET 10.0.401, workload 10.0.401.1, MAUI 10.0.110, MySQL nativo 9.6.0. Esquema 7 confirmado em GET /health/ready. iPhone 18 Pro Simulator / iOS 27.0 / Xcode 27. Android compilado com JDK 21 explícito.

API compilada pelo runner. Builds iOS (iossimulator-arm64) e Android finais: **zero avisos e zero erros**.

## Testes HTTP com banco real

`python3 scripts/test-native-mysql.py`: **27 aprovados, zero falhas, zero ignorados**, em MySQL temporário isolado, encerrado e removido pelo script. O contrato novo falhou antes da implementação porque o detalhe ainda não fornecia os exercícios da revisão original necessários à correção.

Os três testes de WorkoutCorrectionTests verificaram:

- Correção de séries, inclusão de exercício antes omitido e atualização de duração/observações após exclusão da ficha. Volume passou de 400 para 390 kg e contagem de duas para três séries no exemplo do teste. Início, aluno e revisão original permaneceram. Reenvio não aumentou a versão novamente. Reaplicação da migração e outra instância da API recuperaram o registro corrigido.
- Outro aluno, professor e anônimo impedidos de corrigir o treino. Valores inválidos, duração futura e exercício fora da revisão recusados sem gravação parcial; valores e versão permaneceram iguais após as rejeições. Campos extras não conseguiram alterar início, proprietário ou ficha. O registro da outra conta permaneceu intacto.
- Duas correções simultâneas da mesma versão: uma 200 e outra 409. Reenvio da vencedora manteve a versão. Reutilizar seu identificador com outro conteúdo foi recusado. Uma terceira versão persistiu, e repetir a correção antiga ou a criação original não desfez os valores mais novos.

Os 24 testes anteriores também passaram. O runner confirmou Kestrel real: prontidão 503 sem esquema → migração → 200 → reinício da API → 200. O banco de demonstração não foi usado nem apagado pelo runner.

## Fluxo observado no iPhone Simulator

Usadas somente a conta e o treino fictícios de demonstração já existentes, criados no esquema anterior. A atualização para o esquema 7 preservou o registro; ele recebeu a versão inicial de edição sem perder os valores.

1. Sessão de aluno restaurada após instalar o build novo. Treinos salvos → Ver treino apresentou três séries, 00:02:45 e volume de 905 kg.
2. Corrigir treino abriu duração e séries preenchidas, mantendo início em 21/10/2026 às 01:01 e ficha na revisão 2.
3. Duração com 61 segundos foi recusada localmente, antes da confirmação/envio.
4. Corrigidos duração para **00:03:30**, tríceps para **12 × 25 kg**, supino para **10 × 42,5 kg**; removida a segunda série antiga de tríceps após confirmação; adicionada uma segunda série de supino de **6 × 45 kg**. Observação alterada para “Treino demonstrativo corrigido.”
5. Interrompido somente o helper local com SIGINT. Salvar correção mostrou confirmação. Após confirmar, a falha de conexão manteve os campos protegidos e ofereceu Tentar salvar correção novamente e Recarregar registro. A mensagem e as ações foram conferidas com rolagem manual. Captura ios-correcao-sem-conexao.jpg.
6. Reiniciado Executar-local.command. A mesma página reenviou a correção e voltou ao detalhe, com os valores atualizados. Volume recalculado: **12×25 + 10×42,5 + 6×45 = 995 kg**. Início e ficha de origem permaneceram iguais. Captura ios-treino-corrigido.jpg.
7. App encerrado e reaberto. A sessão foi restaurada; Treinos salvos apresentou **um registro**, três séries, 00:03:30 e 995 kg. Ver treino recuperou cargas, repetições e observações corrigidas. Captura ios-correcao-recuperada.jpg.

## Limites

Android: compilação, sem execução nesta etapa. Windows, aparelho físico, Release e avaliação com participantes não executados. Concorrência entre dois editores e repetição após uma resposta perdida foram verificadas pelos contratos HTTP, sem simulação manual entre dois aparelhos. A recuperação de rede no simulador usou serviço indisponível antes do envio, não interrupção de pacotes após o commit.

O botão Recarregar registro e a confirmação de saída/cancelamento não foram percorridos manualmente nesta rodada. Os dados do formulário permanecem somente na página aberta, sem rascunho persistente. A tabela de correções registra identificadores/versões, sem cópia integral das séries antigas ou função de restauração.

Totais por registro foram implementados e conferidos. Indicadores por período, evolução de carga e acompanhamento pelo professor continuam em HIST-01; a avaliação acadêmica com três participantes permanece pendente.
