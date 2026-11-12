# Evidências — adaptação PostgreSQL e Render

Data: 12/11/2026. Base: ZIP Workoutly-documentacao-situacao-fase-2(1).zip enviado pelo usuário. Banco remoto já preparado por ele com os scripts PostgreSQL; nenhum acesso ou alteração ao Render foi feito durante esta implementação.

## Alteração

API usa Npgsql 10.0.3 e PostgreSQL, com UUIDs tipados, filtros nulos tipados, datas UTC/TIMESTAMPTZ, COALESCE, CURRENT_TIMESTAMP, leitura bigint das agregações e SQLSTATE para conflitos. As regras de negócio, hashes de senha, tokens opacos, revisões e autorização foram mantidos. Capturas de conflito dentro de transação encerram a operação e o descarte reverte a transação; nenhuma consulta é executada dentro de transação abortada.

PostgresDatabase aceita conexão Npgsql ou URL PostgreSQL do Render. A URL exige TLS e decodifica usuário/senha escapados. O bootstrap explícito usa transação e advisory lock; no esquema versão 9, é uma operação sem alteração. O startup normal não executa DDL. Os dois recursos SQL embutidos correspondem ao modelo já entregue/importado, com as transações administradas pelo código.

Dockerfile de múltiplas etapas publica apenas a API .NET 10 e executa como usuário não root. A porta vem de PORT; suporte a proxy é habilitado explicitamente por Hosting__RenderProxy. O aplicativo mantém configuração de endereço existente e passa a esperar até 120 segundos pela resposta, sem retentativa automática.

## Verificações executadas

Ambiente Linux x64, SDK .NET 10.0.404, PostgreSQL 18.6 nativo em processo isolado (binários obtidos pelo pacote embedded-postgres), sem uso de dados reais.

- Build da API concluído. Publicação Release (`dotnet publish ... -c Release ... /p:UseAppHost=false`) concluída sem avisos ou erros na execução final.
- Suíte xUnit: **37 aprovados, zero falhas, zero ignorados**, com conexão PostgreSQL local real. Cada classe usa banco aleatório separado, removido ao concluir.
- Inclui cadastro, email duplicado, login/logout, expiração, isolamento entre usuários, catálogo compartilhado/privado, papéis e vínculos, fichas versionadas, sessões, reenvio, correção concorrente e histórico com offset.
- Teste HTTP do proxy foi inicialmente vermelho (HTTPS encaminhado rejeitado) e ficou verde após configurar middleware: modo Render aceita o esquema HTTPS encaminhado; sem habilitação continua rejeitando, e HTTP encaminhado também é rejeitado em Production.
- Smoke test do binário Release em Kestrel/TCP local com PORT definido e **DATABASE_URL**, senha com caracteres escapados e TLS. Banco preenchido usando os SQLs originais 01 e 02, simulando o banco já importado via pgAdmin. A conexão usou papel proprietário da base, sem superusuário.
- /health/ready retornou 200, versão 9. Cadastro via HTTPS encaminhado retornou 201; após reiniciar o processo, /auth/me recuperou a conta com o mesmo token. Catálogo permaneceu com 30 exercícios e descrição personalizada não foi alterada pelo startup.

## Limites

Docker não está disponível neste ambiente: a imagem não foi construída/executada aqui. Os comandos de build/publicação .NET equivalentes foram executados; o build da imagem ainda será comprovado no Render. Não houve deploy em conta do usuário. Android/iOS não foram compilados nem executados, pois o ambiente não possui os respectivos workloads/SDKs. A etapa final é criar o Web Service, configurar DATABASE_URL e Hosting__RenderProxy, conferir /health/ready e testar o app com o endereço HTTPS real.

O teste local não cobre indisponibilidade regional, suspensão do plano gratuito, cadeia de proxies real do provedor ou comportamento do aparelho. Nenhuma credencial do Render está embutida no pacote.
