#!/bin/bash
set -e
COMMIT_ID=$(git rev-parse --short HEAD)
az acr login --name $ACR_NAME
docker buildx build --platform=linux/amd64 ../../src/  -f ../../src/SocialApp.WebApi/Dockerfile --no-cache -t $ACR_URI/socialapp:$COMMIT_ID
docker push $ACR_URI/socialapp:$COMMIT_ID
echo "{\"tag\": \"$COMMIT_ID\"}"