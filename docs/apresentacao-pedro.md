# Dados fictícios para apresentação — Pedro

Preparado em 2026-10-21, no MySQL nativo local usado pela API em http://localhost:5080.

Todos os dados de ficha, medidas e desempenho descritos aqui são fictícios e servem à demonstração. Não são resultados de avaliação com participantes.

## Contas existentes

| Perfil | Nome | E-mail |
| --- | --- | --- |
| Aluno | Pedro | teste@teste.com |
| Professor | Pedro | professor@teste.com |

As senhas existentes foram preservadas. O aluno está vinculado ao professor. As contas anteriores e seus registros foram preservados.

## Perfil fictício do aluno

24 anos, 1,76 m, 78 kg; objetivo: condicionamento e hipertrofia; rotina simulada: três treinos por semana. Esses dados aparecem nas observações das fichas, pois o app atual não possui campos próprios de avaliação física.

## Catálogo e fichas

O professor tem 32 exercícios disponíveis: 30 compartilhados e dois exercícios privados (crucifixo com halteres e elevação lateral). O aluno tem esses 30 compartilhados, dois privados próprios e os dois movimentos privados usados pelo professor nas fichas, totalizando 34 registros por ID no catálogo completo. Os exemplos privados e compartilhados de mesmo nome permanecem separados para preservar fichas e histórico. As três fichas foram criadas pelo professor para o aluno.

### Treino A — Peito, ombros e tríceps

| Exercício | Séries | Repetições por série |
| --- | --- | --- |
| Supino reto | 3 | 12 |
| Crucifixo com halteres | 3 | 12 |
| Desenvolvimento com halteres | 3 | 10 |
| Tríceps na polia | 3 | 12 |

### Treino B — Costas e bíceps

| Exercício | Séries | Repetições por série |
| --- | --- | --- |
| Puxada frontal | 3 | 12 |
| Remada curvada | 3 | 10 |
| Rosca direta | 3 | 12 |
| Elevação lateral | 3 | 12 |

### Treino C — Pernas e panturrilhas

| Exercício | Séries | Repetições por série |
| --- | --- | --- |
| Agachamento livre | 3 | 12 |
| Leg press | 3 | 12 |
| Cadeira extensora | 3 | 12 |
| Elevação de panturrilhas | 3 | 15 |

## Histórico fictício

12 sessões entre 2026-09-24 e 2026-10-19, distribuídas em quatro semanas. As cargas aumentam a cada semana; panturrilhas permanece com 0 kg para representar peso corporal.

Totais conferidos: 12 dias ativos, 144 séries, 1716 repetições, 582 minutos e 37,656 kg de volume acumulado (soma de repetições × carga, não carga máxima).

## Sequência para apresentar

1. Entrar como professor com a conta acima. Abrir **Meus alunos** e selecionar Pedro.
2. Abrir **Fichas do aluno** e mostrar as fichas A, B e C; abrir uma ficha para mostrar exercícios, séries, repetições e as observações fictícias.
3. Abrir **Histórico do aluno**, selecionar os últimos 30 dias e demonstrar frequência, volume e evolução das cargas. O acompanhamento do professor é somente de leitura.
4. Sair e entrar como aluno com a conta acima. Abrir **Meu professor** para mostrar o vínculo.
5. Abrir **Minhas fichas** e os detalhes de um treino em **Histórico e evolução**. Mostrar séries, cargas, duração e observações.
6. Abrir **Explorar exercícios** para mostrar as imagens e o catálogo ampliado. Ativar **Somente meus exercícios**: aparecem 14 registros, incluindo todos os 12 exercícios das fichas e os dois próprios. Iniciar uma ficha pode demonstrar o registro de uma nova sessão; ao salvá-la, os totais passarão a incluir esse novo treino.

## Verificação realizada

- API local com MySQL pronto e esquema versão 8.
- Leitura das três fichas pelas contas de aluno e professor: conteúdos iguais.
- Histórico dos últimos 30 dias pelas duas contas: conteúdos e totais iguais; as 12 sessões foram recuperadas.
- Detalhes de uma sessão consultados pelas duas contas: iguais.
- Sessões administrativas temporárias usadas na preparação foram removidas; as sessões existentes do aplicativo foram preservadas.
- A interface do professor estava aberta no iPhone 18 Pro com iOS 27. A navegação completa com os novos dados não foi confirmada visualmente nesta preparação. Não foram alteradas telas ou regras do aplicativo.

Os registros persistem na instância nativa `.local/native-mysql/data`; não foram inseridos na instância Docker ou em servidor remoto. Para iniciar o mesmo ambiente posteriormente: `python3 scripts/dev-native.py` (sem abrir outra instância enquanto já estiver em execução).
