# Catálogo e exercícios próprios

O catálogo identifica os movimentos que serão usados nas fichas. Cada exercício contém nome, grupo muscular, equipamento e descrição opcional. Séries, repetições e carga pertencem ao planejamento ou ao treino realizado, e por isso não são campos do catálogo.

## Como usar

1. Inicie o ambiente com `Executar-local.command` e abra o app no simulador.
2. Entre na conta e toque em **Explorar exercícios**.
3. Busque por nome, grupo ou equipamento. A busca ignora diferenças de maiúsculas e acentos e filtra os dados carregados; **Atualizar** consulta novamente a API.
4. Toque em um exercício para consultar os detalhes. O catálogo compartilhado tem 30 movimentos ilustrados. **Meu exercício** identifica registros cadastrados pela conta; **Na minha ficha** identifica movimentos das fichas atuais, inclusive os disponibilizados pelo professor.
5. Use **Novo exercício**, preencha os campos e toque em **Salvar exercício**. A lista é recarregada após salvar; **Somente meus exercícios** mostra os registros cadastrados pela conta e os exercícios de todas as suas fichas ativas, sem duplicar os itens repetidos.

Os valores do catálogo servem para identificar registros, sem gerar treinos ou prescrições. A seleção para compor uma ficha está disponível ao montar ou editar fichas. Edição e exclusão de exercícios não fazem parte deste incremento.

## Ilustrações e referências rápidas

As ilustrações SVG são recursos locais, sem download durante o uso e sem vínculo com dados pessoais. Elas vêm do projeto aberto [Workout Guide](https://github.com/bryllim/workout-guide), de Bryl Lim, sob [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/); as atribuições e os links de cada arquivo estão em [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). `Services/ExerciseVisuals.cs` associa os 30 IDs estáveis às imagens e dicas. Nomes exatos dos novos movimentos também são reconhecidos nos exemplos privados já cadastrados. Outros cadastros recebem um ícone genérico de exercício, sem instrução inferida. As imagens aparecem na lista, no seletor de ficha e nos detalhes. O nome do criador foi retirado da tela principal de detalhes; a atribuição completa está acessível em **Créditos e licenças**, com leitura local.

As referências são apenas apoio visual para identificar o movimento. Não representam prescrição, carga, amplitude adequada para cada pessoa nem substituem orientação profissional.

## Contrato HTTP

As três rotas exigem `Authorization: Bearer <token>` válido. O identificador do usuário vem da sessão validada, nunca de parâmetros enviados pelo app.

| Método e rota | Resultado |
| --- | --- |
| `GET /exercises` | 200: catálogo compartilhado, exercícios próprios e cópias dos exercícios das revisões atuais das fichas ativas da conta. |
| `GET /exercises/{id}` | 200: exercício compartilhado, próprio ou recebido em ficha ativa; 404 para inexistente ou privado de outra conta sem essa disponibilização. |
| `POST /exercises` | 201 com exercício criado e header Location; 400 para campos inválidos. Sempre cria um exercício privado. |

Sem sessão ou com sessão expirada, as rotas retornam 401. Falha de banco retorna 503 com mensagem genérica. Fora de Development, a API exige HTTPS, assim como o app em Release. Respostas dessas rotas usam `Cache-Control: no-store`.

Corpo de criação:

```json
{
  "name": "Remada unilateral",
  "muscleGroup": "Costas",
  "equipment": "Halteres",
  "description": "Variação identificada pelo usuário."
}
```

Nome: 2–120 caracteres; grupo: 1–60; equipamento: 1–80; descrição opcional: até 1000. Espaços nas extremidades são removidos e campos obrigatórios em branco são rejeitados. A resposta inclui `id` e `isShared`; não expõe o proprietário. Grupo e equipamento são textos livres para permitir variações próprias. Nomes repetidos são permitidos, pois podem identificar variações; não há reenvio automático do POST. O botão fica bloqueado durante a requisição.

## Conceito, código e decisão

- **Catálogo compartilhado e propriedade:** `Data/Migrations/003_exercises.sql` cria a tabela `exercises`. `owner_id` nulo identifica um exercício inicial; nos privados, a chave estrangeira referencia `users`. Seeds com IDs fixos evitam duplicação ao reaplicar a migração. As migrações 001 e 002 permanecem preservadas.
- **Autorização:** `Exercises/ExerciseEndpoints.cs` exige sessão; `ExerciseService.cs` filtra cada consulta por catálogo compartilhado ou proprietário autenticado. A consulta de detalhes usa o mesmo filtro da listagem. Um ID conhecido não concede acesso aos dados de outro usuário.
- **Interface e comunicação:** `ExercisesPage.xaml` apresenta a lista e a busca; `ExercisePage.xaml` exibe detalhes ou cadastro. Seus code-behinds controlam carregamento e navegação. `Services/AccountClient.cs` usa a sessão segura já existente para enviar HTTP e serialização gerada para compatibilidade com o app nativo.
- **Mídia local:** os SVGs licenciados em `Resources/Images/Exercises/` são empacotados no MAUI. A imagem é escolhida apenas no cliente a partir do ID já retornado pela API; não foi necessário acrescentar coluna, migração ou rota de mídia.
- **Validação no servidor:** os limites são aplicados na API mesmo que o cliente seja outro. Uma falha de conexão mantém os campos preenchidos para nova tentativa; não há fila nem sincronização offline.

Os caminhos acima são relativos a `src/Workoutly.Api` e `src/Workoutly.Mobile`, conforme o componente. A separação permite testar acesso e persistência pela API sem depender do simulador.

## Verificar

Execute `python3 scripts/test-native-mysql.py`. Os testes HTTP de exercícios verificam catálogo autenticado, migração reaplicável, cadastro, limites dos campos, rejeição sem persistir dados inválidos, recuperação em outra instância da API e isolamento entre duas contas. O MySQL temporário é separado do banco usado para demonstração.

As observações de execução no aparelho e os limites ficam em [evidências deste incremento](evidencias/exercicios.md).

## Regra do filtro

A API retorna `isShared`, `isOwned` e `isInPlan`. O filtro usa `isOwned || isInPlan`: pertencer ao catálogo compartilhado não exclui um item que esteja numa ficha. Exercícios privados recebidos são lidos da cópia na revisão atual da ficha; o catálogo privado completo do professor não é exposto. Revisões antigas e fichas excluídas não entram nesse filtro. Excluir um vínculo com o professor preserva as fichas e seus exercícios para o aluno. O esquema 8 amplia o catálogo sem alterar os registros particulares existentes.

## Músculos trabalhados

As 30 descrições compartilhadas agora identificam músculos principais, auxiliares e estabilizadores, conforme a variante. A migração 009 aplica o conteúdo uma única vez e estabelece o esquema 9. A revisão e as fontes estão em [Revisão muscular](revisao-musculos-exercicios.md), com dados estruturados em [catalogo-musculos.json](catalogo-musculos.json). Exercícios privados existentes no ambiente de demonstração também foram revisados; nomes genéricos de remada recebem uma explicação condicional, sem presumir a variação. Novos exercícios privados continuam dependendo da descrição informada pelo usuário.

As fichas locais receberam novas revisões, preservando os dados históricos. O catálogo representa a descrição atual; sessões antigas continuam vinculadas ao texto da revisão executada.
