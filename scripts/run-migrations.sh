#!/bin/bash

# Script para aplicar migrations em ambientes de homologação/produção
# USO: ./run-migrations.sh <projeto> <ambiente>
# EXEMPLO: ./run-migrations.sh Catalog.Api production

PROJECT=$1
ENVIRONMENT=${2:-staging}

if [ -z "$PROJECT" ]; then
    echo "❌ Erro: Especifique o projeto"
    echo "Uso: ./run-migrations.sh <projeto> <ambiente>"
    echo "Exemplo: ./run-migrations.sh Catalog.Api production"
    exit 1
fi

PROJECT_PATH="../src/${PROJECT}"

if [ ! -d "$PROJECT_PATH" ]; then
    echo "❌ Erro: Projeto ${PROJECT} não encontrado em ${PROJECT_PATH}"
    exit 1
fi

echo "🔍 Verificando migrations pendentes para ${PROJECT}..."
dotnet ef migrations list -p "$PROJECT_PATH"

echo ""
echo "⚠️  ATENÇÃO: Você está prestes a aplicar migrations no ambiente: ${ENVIRONMENT}"
echo "📁 Projeto: ${PROJECT}"
echo ""
read -p "Deseja continuar? (yes/no): " confirmation

if [ "$confirmation" != "yes" ]; then
    echo "❌ Operação cancelada"
    exit 0
fi

echo "🚀 Aplicando migrations..."
dotnet ef database update -p "$PROJECT_PATH"

if [ $? -eq 0 ]; then
    echo "✅ Migrations aplicadas com sucesso!"
else
    echo "❌ Erro ao aplicar migrations"
    exit 1
fi
