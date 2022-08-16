#!/bin/bash

read -r VERSIONPREFIX<version.txt
git tag -a v$VERSIONPREFIX -m "Version $VERSIONPREFIX"
git push origin v$VERSIONPREFIX