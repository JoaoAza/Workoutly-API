# Verificação do incremento MySQL

Data: 20/10/2026. Branch: desenvolvimento/mysql. Marco: v0.2.0-mysql.

## Ambiente e escopo

Mac Apple Silicon, SDK .NET 10.0.401, runtime .NET 10.0.12, MySqlConnector 2.6.2, MySQL nativo 9.6.0 (Homebrew). Nenhuma alteração no aplicativo MAUI e nenhum banco existente foi utilizado.

A validação iniciou mysqld com --no-defaults, diretório temporário próprio, socket Unix e --skip-networking. O servidor temporário e os processos HTTP foram encerrados ao final; os arquivos temporários foram removidos. Não há servidor MySQL permanente deste projeto rodando como resultado desse teste.

## Resultados

| Verificação | Resultado observado |
| --- | --- |
| Testes antes da implementação | Três casos de readiness falharam com HTTP 404, pois a rota não existia. |
| Compilação | API e testes compilados sem avisos na execução final. |
| Contratos sem banco | Quatro casos aprovados: liveness, ausência de configuração, configuração inválida e servidor inacessível. |
| MySQL real | Cinco testes no total aprovados, zero falhas e zero ignorados, incluindo o bootstrap. |
| Antes da migração | Kestrel real respondeu HTTP 503, database=schema_pending. |
| Comando --migrate | Executou o SQL embutido e informou sucesso. |
| Após migração | Kestrel respondeu HTTP 200, database=ready. |
| Reinício da API | Processo encerrado e novo processo iniciado; HTTP 200 continuou retornando a versão gravada no MySQL. |
| Reaplicação | Aplicar a preparação duas vezes não falhou; outra instância da API recuperou schemaVersion=1. |
| Falha da CLI | Sem conexão configurada, --migrate retornou código 1 e mensagem genérica, sem dados da conexão. |
| Configuração local | .env com valores aleatórios ignorado pelo Git; compose.yaml passou em docker compose config --quiet. |
| Documentos originais | Os quatro SHA-256 permanecem iguais aos registrados na fundação. |

## Reproduzir

```sh
python3 scripts/test-native-mysql.py
```

Esse comando requer MySQL nativo no PATH. No ambiente Docker, seguir docs/mysql.md e usar python3 scripts/dev.py test-db.

## Limites

O Docker Desktop não completou a configuração inicial com privilégios do macOS. O container não foi iniciado, portanto a validação do Compose foi apenas sintática; a evidência de persistência veio do MySQL nativo real, da mesma versão declarada na imagem. Nenhuma configuração privilegiada do Docker foi concluída por este trabalho.

O dado persistido neste incremento é a versão da migração, não uma conta ou sessão de treino. Tabelas do domínio, autenticação e comunicação do app com a API continuam pendentes. Os testes HTTP não comprovam a usabilidade, segurança de autenticação futura ou funcionamento em dispositivos. Nenhum teste com participantes foi realizado.
