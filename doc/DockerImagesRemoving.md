# Copyright (c) 2020 Bitcoin Association

# Cleaning up docker containers and removing DARA applications

For cleaning up docker containers and removing DARA applications from the computer follow this step

***warning**: following this steps will delete applications, logs and data*

* remove docker containers and logs for BlacklistManager by executing this command in folder where `docker-compose.yml` file for BlacklistMnager is

```bash
docker-compose down
```

* remove docker volumes / data for  BlackListManager apps by using this commands

use this command to list all docker volumes

```bash
docker volume ls
```

use this command to delete  docker volumes that end with `bmdatavolume`

```bash
docker volume rm [volumename]
```
