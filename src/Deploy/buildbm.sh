#!/bin/bash

git remote update
git pull
git status -uno

read -r VERSIONPREFIXBM<version.txt

COMMITID=$(git rev-parse --short HEAD)

APPVERSIONBM="$VERSIONPREFIXBM-$COMMITID"

echo "***************************"
echo "***************************"
echo "Building docker image for BM app version $APPVERSIONBM"
read -p "Continue if you have latest version (commit $COMMITID) or terminate job and get latest files."

mkdir -p Build/bm

sed s/{{VERSIONBM}}/$APPVERSIONBM/ < template_bm_docker-compose.yml > Build/bm/docker-compose.yml

cp template_bm.env Build/bm/.env

docker build  --build-arg APPVERSION=$APPVERSIONBM -t blacklistmanager:latest -t blacklistmanager:$APPVERSIONBM -f ../BlacklistManager/BlacklistManager.API.Rest/Dockerfile ..

docker save blacklistmanager:$APPVERSIONBM > Build/bm/blacklistmanagerapp.tar

zip -j Build/BM.zip Build/bm/*.* Build/bm/.env
