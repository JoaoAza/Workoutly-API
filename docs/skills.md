# Skills locais do projeto

As skills foram instaladas com o Skill Installer em `.agents/skills`, incluindo os recursos das pastas de origem. São arquivos locais versionados; não se atualizam automaticamente. Não instalar também o mesmo pacote globalmente ou por plugin para evitar duplicatas.

| Origem | Revisão fixada | Skills |
| --- | --- | --- |
| https://github.com/mattpocock/skills | 5beb35b040e3de97e54fa9e386d7e42d15c295c2 | setup-matt-pocock-skills, grill-with-docs, domain-modeling, to-tickets, tdd, diagnosing-bugs, codebase-design, code-review, grilling, writing-for-agents |
| https://github.com/cathrynlavery/diagram-design | f2f4d49ea8d632f17cf196ba26599277a93f767b | diagram-design |

As dependências internas diretas de grill-with-docs (grilling e domain-modeling) e a referência usada por tdd (codebase-design) estão incluídas. Não foi instalado um orquestrador de implementação completo.

## Quando usar

- **Requisitos:** pedir “use grill-with-docs para esclarecer esta mudança”; ela combina entrevista e documentação do domínio. Há perguntas deliberadas nesse fluxo: usar apenas quando houver decisões relevantes abertas.
- **Tarefas:** to-tickets ajuda a dividir uma mudança, usando o tracker local documentado em agents/issue-tracker.md.
- **Implementação:** tdd orienta testes de comportamento; codebase-design ajuda a escolher limites entre componentes.
- **Falhas:** diagnosing-bugs orienta investigação reproduzível.
- **Revisão:** code-review compara mudanças com uma referência Git e uma especificação, usando dois subagentes. Informar a referência ao solicitar a revisão.
- **Diagramas:** diagram-design gera HTML/SVG. Antes do primeiro diagrama, configurar o perfil visual do Workoutly ou escolher explicitamente o padrão, conforme a skill. Nenhum diagrama foi gerado nesta etapa.

As skills serão descobertas no próximo turno do Codex dentro desta pasta. A instalação não executa automaticamente seus workflows nem verifica a qualidade do produto. Ler o SKILL.md relevante antes de usar; não executar scripts de terceiros sem avaliar o que fazem.

## Configuração inicial

As instruções do projeto e do tracker foram escritas diretamente a partir das escolhas já feitas pelo usuário: AGENTS.md, GLOSSARY.md, docs/agents/ e docs/backlog.md. A skill setup-matt-pocock-skills está disponível para reconfiguração futura; seu workflow não foi executado nesta etapa. Não há tracker externo ou labels de triagem.

Licenças MIT de origem preservadas em docs/licenses/. Não incorporar estes arquivos ao pacote mobile. Registro detalhado de origem em skills.lock.json.
