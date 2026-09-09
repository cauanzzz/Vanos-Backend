# Verificação do Vanos MVP

Preparado para a API do commit 57495d754d8d59bb46df32b42ca52c782b6d71e3.
Este pacote adiciona testes, solução e workflow. Não substitui nenhum arquivo da API.

## Estado

Os testes NÃO foram executados pelo assistente. O ambiente não dispõe de dotnet/Docker,
e a integração GitHub recusou a gravação com HTTP 403 (Resource not accessible by integration).
Não houve branch, commit, push ou alteração da master nesta tentativa.

## Caminho mais simples: GitHub Actions

Extraia o conteúdo deste ZIP na raiz do seu clone (a pasta que contém README.md e .git).
A estrutura precisa conter `.github/workflows/verify-mvp.yml` e
`Desktop/Vanos/Vanos.API.Tests`, ao lado da API existente em `Desktop/Vanos/Vanos.API`.
Não extraia os testes dentro da pasta Vanos.API: o SDK incluiria esses arquivos no projeto da API.

No terminal, a partir da raiz do clone:

```bash
git switch -c codex/verify-vanos-mvp-tests
git add .github/workflows/verify-mvp.yml Desktop/Vanos/Vanos.API.Tests Desktop/Vanos/Vanos.sln TESTAR-MVP.md
git commit -m "test: add isolated MVP verification"
git push -u origin codex/verify-vanos-mvp-tests
```

Abra https://github.com/cauanzzz/Vanos-Backend/actions e procure `Verify Vanos MVP`.
O push nessa branch dispara restore, build e testes em um runner temporário.
O workflow inicia SQL Server 2022 em container, usando somente credenciais descartáveis de CI.
Nenhum dado ou segredo do seu SQL Server local é necessário.
A master permanece intacta. Se a política do repositório bloquear Actions, o proprietário precisa
habilitar a execução antes de haver resultados; não há testes aprovados enquanto não houver uma execução.

Quando terminar, compartilhe o link da execução para análise dos resultados. O workflow também
anexa o relatório TRX como `vanos-test-results`, quando disponível.

## O que será testado

- Compilação da API atual e do projeto de testes.
- Cadastro Parent/Student/Driver, hashing e roles do JWT.
- 401/403 via HTTP, propriedade dos alunos e invalidação de role no banco.
- Filtros/paginação do marketplace e capacidade da van.
- Propriedade das faturas, simulação repetida/concorrente e bloqueio fora de Development.
- SignalR com cliente real usando Long Polling: consumidor não publica; apenas a conta vinculada
  recebe; remover o vínculo interrompe atualizações futuras; coordenadas inválidas são recusadas.
- SQL Server: migrations a partir de banco vazio, ausência de mudanças de model sem migration,
  pagamentos concorrentes e disputa por uma vaga (a transação vítima de deadlock é revertida).

## Executar localmente

Com o SDK .NET 10:

```bash
cd Desktop/Vanos
dotnet restore Vanos.sln
dotnet test Vanos.API.Tests/Vanos.API.Tests.csproj --logger "trx;LogFileName=verification.trx"
```

Sem a variável VANOS_TEST_SQLSERVER, o teste específico de SQL Server será marcado como ignorado;
os demais usam SQLite em memória. Para executar esse teste localmente, configure essa variável
com uma conexão para um servidor de DESENVOLVIMENTO com permissão de criar bancos. O teste
substitui o nome do banco por um nome aleatório VanosVerify_..., aplica as migrations e remove
somente esse banco temporário no fim. Não use credenciais ou servidor de produção.

## Limites

Esta suíte é uma base de validação. Não cobre carga prolongada, múltiplas instâncias de SignalR,
WebSockets em um proxy real, frontend móvel, cobrança real ou todos os fluxos do produto.
