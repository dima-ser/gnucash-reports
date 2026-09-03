Use to run the container locally for testing. Update `appsettings.Docker.json` with your config

```bash
docker run --rm -it -p 8080:8080/tcp \
-v /home/dima/gnucash.sqlite.gnucash:/app/sqlite/gnucash.sqlite:ro \
-v /home/dima/repos/gnucash-reports/GnuCashReports/appsettings.Docker.json:/app/appsettings.json \
--env TZ=America/Los_Angeles dimaser/gnucashreports:latest