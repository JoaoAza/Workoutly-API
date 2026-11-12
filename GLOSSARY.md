# Vocabulário do Workoutly

| Termo | Significado no projeto |
| --- | --- |
| Usuário | Pessoa que possui conta e acessa os próprios dados. |
| Exercício | Movimento do catálogo ou cadastrado pelo usuário. |
| Ficha | Planejamento com exercícios e séries/repetições de referência. |
| Sessão | Execução de um treino em uma data, com dados realmente realizados. |
| Série | Registro de carga e repetições de um exercício durante a sessão. |
| Carga | Peso registrado em kg; não é o peso corporal do usuário. |
| Volume | Soma de carga × repetições das séries consideradas. |
| Frequência | Quantidade de sessões em um intervalo informado. |
| Evolução | Comparação dos registros do próprio usuário ao longo do tempo; não é avaliação clínica. |
| MVP | Menor versão que atende ao escopo acadêmico e pode ser avaliada. |
| Evidência | Registro verificável vinculado à versão, ambiente e procedimento. |
| Sessão de acesso | Token que autentica a conta por até sete dias; diferente de uma sessão de treino. |
| Logout | Revogação da sessão de acesso atual no MySQL e remoção do token do aparelho. |
| Aluno | Perfil student, escolhido no cadastro público; acessa os próprios registros e pode se vincular a um professor. |
| Professor | Perfil teacher, também disponível no cadastro público; administra o vínculo dos próprios alunos. Gerencia fichas e consulta sessões/indicadores dos alunos atualmente vinculados; não corrige sessões deles. |
| Vínculo | Associação voluntária do aluno com um professor por código; neste MVP, um professor ativo por aluno. Pode ser removida sem apagar contas ou registros. |
| Revisão da ficha | Versão imutável do planejamento, com cópia dos exercícios, séries e repetições previstas. |
| Exclusão lógica | Retira a ficha do uso sem apagar versões e itens necessários à preservação dos registros. |
| Série realizada | Repetições e carga efetivamente informadas pelo aluno; só entra no registro se marcada como feita. |
| Idempotência | Repetir o mesmo ID e conteúdo de uma tentativa recupera o registro existente, sem duplicar ou sobrescrever o treino. |
| Versão do registro | Número de edição do treino salvo, diferente da revisão da ficha usada como origem. |
| Identificador da correção | editId que permite reconhecer uma edição já aceita e repetir a tentativa sem sobrescrever outra versão. |
