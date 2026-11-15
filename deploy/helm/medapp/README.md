# MedApp's Helm chart

Step 2 of the Kubernetes plan (`docs/todo.md`). It deploys:
- **The API** from its image (`MedApp.API/Dockerfile`): it serves the React app, renders its pages with Node and answers `/api`, including the notifications WebSocket.
- **SQL Server** in the cluster (`sql.enabled`, on by default), or an **external database** such as Azure SQL.
- **An ingress** in front of the API (`ingress.enabled`).
- **The EF migrations** as a Job, from the migrations bundle in the same image (`/app/efbundle`).

The quickest way to see it running is the local kind cluster: `deploy/kind/up.sh` installs this chart with `deploy/kind/values.yaml`. See `deploy/kind/README.md`.

## Installing

Build and push the image somewhere the cluster can pull from, then:

```bash
helm upgrade --install medapp deploy/helm/medapp --namespace medapp --create-namespace \
  --set image.repository=<registry>/medapp-api --set image.tag=<tag> \
  --set ingress.enabled=true --set ingress.host=<host> --wait
```

`image.tag` is required, and never `latest`, so each release names exactly what it runs. Every setting is in `values.yaml`, with what it does.

## The database

- **SQL Server in the cluster** (the default): one pod with its data on a volume. The chart makes the `<release>-db` Secret with a random `sa` password and the API's connection string. It keeps the password across upgrades, and the Secret survives `helm uninstall`, like the volume, so a reinstall can still open the data. Delete both by hand to start again. `sql.edition` is `Developer`, which isn't licensed for production.
- **An external database:** set `sql.enabled=false` and `database.existingSecret` to a Secret you've made, with the connection string under `connection-string`. The migrations Job and the API both use it.

## Migrations

The migrations Job is a Helm hook:
- **Before each upgrade:** the schema is migrated before the new API rolls out, and a failed migration fails the upgrade, so the old API keeps running.
- **After the first install:** once the chart's SQL Server exists, so on a new database the API starts before the schema does, briefly.

The Job is deleted when it succeeds. A failed one stays, so you can read its logs (`kubectl logs job/medapp-migrations`), until the next run replaces it.

Because migrations run before the new API is up, a migration that removes or renames something the old API still uses breaks it during the rollout. Make such changes in two releases: first add the new thing, then remove the old.

## The first sign-in

The app seeds no accounts, so a new database has no way to sign in. Locally, `deploy/kind/up.sh --demo-data kind-demo` restores the synthetic demo data with its `admin` login. A real environment needs its first admin made deliberately; that's part of the security step.

## Not yet (later steps in `docs/todo.md`)

- **One copy of the API only** (`api.replicas`): more need a SignalR backplane, so live notifications reach every copy, and shared data-protection keys.
- **Health checks:** the probes only check that the port is open, because `/health` and `/alive` are mapped only in Development.
- **Security:** secrets from a secret store, TLS certificates (cert-manager), network policies and read-only filesystems. The containers already run as non-root users, with no privilege escalation and no Linux capabilities (except the one `sqlservr` needs).
- **Argo CD:** the chart's `lookup` of its own Secret doesn't work under Argo CD, which renders charts without access to the cluster. There, use `database.existingSecret`.
