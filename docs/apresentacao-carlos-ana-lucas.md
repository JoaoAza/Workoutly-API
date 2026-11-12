# Grupo fictício de apresentação — Carlos, Ana e Lucas

Criado no banco local do Workoutly em 21/10/2026. Os dados pessoais, fichas, cargas e desempenhos são fictícios, destinados à apresentação acadêmica.

## Acessos

| Perfil | Nome | E-mail |
| --- | --- | --- |
| Professor | Carlos | carlos@teste.com |
| Aluno | Ana | ana@teste.com |
| Aluno | Lucas | lucas@teste.com |

As credenciais de demonstração estão apenas em `.local/apresentacao/acessos-grupo-carlos.json`, ignorado pelo Git. Os dois alunos estão vinculados ao professor Carlos.

## Dados preparados

Cada aluno tem duas fichas com oito exercícios em cada uma, totalizando 16 exercícios distintos no filtro **Somente meus exercícios**, e quatro sessões fictícias nas últimas duas semanas. O catálogo compartilhado oferece os 30 exercícios ilustrados com descrições musculares revisadas.

| Aluno | Ficha | Exercícios |
| --- | --- | --- |
| Ana | A — membros superiores | Supino com halteres; crucifixo com halteres; puxada frontal; remada baixa; desenvolvimento com halteres; elevação lateral; rosca martelo; tríceps na polia. |
| Ana | B — membros inferiores | Agachamento livre; leg press; cadeira extensora; cadeira flexora; levantamento terra romeno; elevação pélvica; afundo com deslocamento; elevação de panturrilhas. |
| Lucas | A — membros superiores | Supino reto; supino inclinado; barra fixa; remada unilateral; desenvolvimento com halteres; elevação frontal; rosca Scott; tríceps testa. |
| Lucas | B — membros inferiores | Agachamento búlgaro; leg press; cadeira extensora; mesa flexora; levantamento terra romeno; elevação pélvica; afundo com deslocamento; panturrilha sentado. |

As observações incluem um perfil fictício para cada aluno: Ana, 25 anos, 1,65 m e 62 kg; Lucas, 28 anos, 1,80 m e 82 kg. As fichas e cargas não são prescrições individuais.

## Como apresentar

1. Entrar como Carlos e abrir **Meus alunos**: Ana e Lucas aparecem vinculados.
2. Abrir as fichas de cada aluno e mostrar os oito exercícios, séries, repetições e descrições.
3. Consultar o histórico de cada aluno nos últimos 30 dias, mostrando as quatro sessões e indicadores.
4. Entrar como Ana ou Lucas para mostrar **Meu professor**, **Minhas fichas** e o filtro de exercícios próprios com 16 itens.

## Verificações

Preparação realizada pelos contratos HTTP normais de cadastro, vínculo, fichas e sessões. Confirmados os papéis persistidos, os dois vínculos, duas fichas por aluno e oito exercícios por ficha. As consultas de fichas e histórico retornaram conteúdos iguais para professor e respectivo aluno. Todos os exercícios das fichas aparecem no filtro do aluno, e suas descrições contêm os músculos trabalhados. O novo professor não consegue consultar as fichas do Pedro, vinculado a outro professor.

Não houve alteração de código ou das contas, vínculos, fichas e sessões existentes. As sessões de acesso usadas na preparação foram encerradas. A verificação desta preparação ocorreu pela API, sem trocar a conta aberta no simulador.
