# Guia de Migrations - Entity Framework Core

## 🎯 Estratégia por Ambiente

### Development (Desenvolvimento Local)
✅ **Migrations automáticas habilitadas**
- Migrations são aplicadas automaticamente na inicialização do app
- Código: `if (app.Environment.IsDevelopment()) { await dbContext.Database.MigrateAsync(); }`
- Seguro para experimentação e testes

### Staging/Homologação
⚠️ **Migrations manuais com aprovação**
- Usar script `scripts/run-migrations.sh`
- Requer confirmação manual
- Testar antes de aplicar em produção

### Production (Produção)
🔒 **Migrations manuais com processo formal**
- Aplicar em janela de manutenção
- Fazer backup do banco antes
- Usar migrations bundle (recomendado)
- Nunca aplicar migrations automáticas

## 📋 Comandos Essenciais

### Criar nova migration
```bash
dotnet ef migrations add NomeDaMigration -p src/Catalog.Api
```

### Listar migrations
```bash
dotnet ef migrations list -p src/Catalog.Api
```

### Aplicar migrations manualmente
```bash
dotnet ef database update -p src/Catalog.Api
```

### Reverter última migration
```bash
dotnet ef migrations remove -p src/Catalog.Api
```

### Gerar script SQL (sem aplicar)
```bash
dotnet ef migrations script -p src/Catalog.Api -o migration.sql
```

## 🎁 Migrations Bundle (Recomendado para Produção)

### 1. Criar o bundle
```bash
dotnet ef migrations bundle -p src/Catalog.Api --self-contained -r linux-x64
```

### 2. Executar o bundle em produção
```bash
./efbundle --connection "Server=prod-server;Database=catalogdb;..."
```

**Vantagens:**
- Não precisa do .NET SDK instalado no servidor
- Executável standalone
- Mais seguro e controlado

## ⚠️ Boas Práticas

### ✅ Fazer

1. **Sempre fazer backup antes de aplicar migrations em produção**
   ```bash
   # Exemplo SQL Server
   BACKUP DATABASE catalogdb TO DISK = 'backup_before_migration.bak'
   ```

2. **Testar migrations em ambiente de staging primeiro**

3. **Revisar o SQL gerado antes de aplicar**
   ```bash
   dotnet ef migrations script > review.sql
   # Revisar o arquivo review.sql
   ```

4. **Usar migrations reversíveis quando possível**
   - Implementar método `Down()` nas migrations
   - Permite rollback em caso de problemas

5. **Aplicar migrations em janela de manutenção**
   - Minimiza impacto em usuários
   - Permite rollback se necessário

6. **Versionar migrations no Git**
   - Mantém histórico de mudanças no schema
   - Facilita troubleshooting

### ❌ Nunca fazer

1. **Modificar migrations já aplicadas em produção**
   - Crie uma nova migration para correções

2. **Aplicar migrations automáticas em produção**
   ```csharp
   // ❌ PERIGOSO EM PRODUÇÃO
   await dbContext.Database.MigrateAsync();
   ```

3. **Deletar dados sem backup**
   - Sempre faça backup antes de migrations destrutivas

4. **Aplicar múltiplas migrations grandes de uma vez**
   - Divida em migrations menores e incrementais

5. **Ignorar warnings do EF Core**
   - Warnings geralmente indicam problemas potenciais

## 🔄 Workflow Recomendado

### Desenvolvimento
```bash
# 1. Fazer mudanças no modelo/DbContext
# 2. Criar migration
dotnet ef migrations add AddNewFeature -p src/Catalog.Api

# 3. Revisar código gerado em Migrations/
# 4. Testar localmente (auto-migration vai aplicar)
dotnet run --project src/MicroservicesAspire.AppHost
```

### Homologação/Produção
```bash
# 1. Gerar script SQL
dotnet ef migrations script -p src/Catalog.Api -o migration_v2.sql

# 2. Revisar SQL manualmente
cat migration_v2.sql

# 3. Fazer backup do banco
# (comando específico do banco de dados)

# 4. Aplicar em janela de manutenção
dotnet ef database update -p src/Catalog.Api
# OU usar o migrations bundle
./efbundle --connection "connection-string"

# 5. Validar aplicação funcionando
# 6. Monitorar por problemas
```

## 🚨 Troubleshooting

### Migration falha ao aplicar
```bash
# Ver último migration aplicado
dotnet ef migrations list -p src/Catalog.Api

# Reverter para migration específico
dotnet ef database update NomeMigrationAnterior -p src/Catalog.Api
```

### Tabela não existe após migration
```bash
# Verificar se migration foi aplicado
dotnet ef migrations has-pending-model-changes -p src/Catalog.Api

# Forçar aplicação
dotnet ef database update -p src/Catalog.Api --verbose
```

### Migration criado com modelo errado
```bash
# Remover última migration (se ainda não aplicado)
dotnet ef migrations remove -p src/Catalog.Api

# Corrigir modelo e criar novamente
dotnet ef migrations add NomeCorrigido -p src/Catalog.Api
```

## 📚 Referências

- [EF Core Migrations - Microsoft Docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Applying Migrations - Microsoft Docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Migrations Bundle](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#bundles)
