docker_login:
	$(eval acr_name := $(shell cd ./terraform/ && terraform output -raw acr_name))
	az acr login --name ${acr_name}

docker_build_and_push:
	$(eval commit := $(shell git rev-parse --short HEAD))
	$(eval acr_name := $(shell cd ./terraform/ && terraform output -raw acr_name))
	docker buildx build --platform=linux/amd64 ../src/  -f ../src/SocialApp.WebApi/Dockerfile --no-cache -t $(acr_name).azurecr.io/socialapp:$(commit)
	docker push $(acr_name).azurecr.io/socialapp:$(commit)