# WebGL deploy

Static hosting for the Unity WebGL client. The game build is **not** baked into an
image: an nginx container serves a host directory, and the deploy script uploads
new builds into it over SSH.

```
/data/mines-leader-web/          <- bind-mounted into the container as /srv
├── nginx.conf                   <- uploaded by the deploy script, mounted into nginx
├── releases/
│   ├── 20260903-011500-01271762/
│   └── 20260903-093000-a1b2c3d4/
└── current -> releases/20260903-093000-a1b2c3d4   <- nginx root is /srv/current
```

## One-time server setup

1. `ssh <host> 'sudo mkdir -p /data/mines-leader-web/releases && sudo chown -R $USER /data/mines-leader-web'`
2. Run the deploy script once (below). The compose file bind-mounts
   `nginx.conf` and `current` from that directory, and Docker turns a *missing*
   bind source into an empty directory — the container then fails to start with
   "not a directory". So the files must exist first.
3. Coolify → New Resource → **Docker Compose (Empty)**, pasting the contents of
   `docker-compose.yaml` from this directory. Keep the file here as the source of
   truth and re-paste it when it changes — the service has no repository checkout,
   which is exactly why nginx.conf is uploaded rather than mounted from the repo.
   - Destination: `mines-leader-production` (same as the backend)
   - Domain: set `SERVICE_FQDN_WEB_80=https://play.minesleader.xyz`, or let Coolify
     generate one.
4. Deploy.

## One-time local setup

Copy `deploy.env.example` to `~/.config/mines-leader/deploy.env` and fill in the SSH
target.

Authentication is either of:

- **Password** — put it (and nothing else) in `tools/scripts/.deploy-password`, which
  is gitignored and read via `sshpass -f`, so it never reaches the process list.
  Needs the `sshpass` package installed locally.
- **Key** — set `DEPLOY_SSH_KEY` in `deploy.env`, or rely on the ssh-agent default.
  Used whenever the password file is absent.

## Deploying

```bash
tools/scripts/deploy-webgl.sh            # upload client/build/build as a new release
tools/scripts/deploy-webgl.sh --build    # build in Unity batchmode first
tools/scripts/deploy-webgl.sh --list     # what is on the server, and what is live
tools/scripts/deploy-webgl.sh --rollback 20260903-011500-01271762
```

The upload goes into a fresh release directory and only then flips `current`, so
players never see a half-uploaded build. Unchanged payloads are hardlinked against
the previous release, so a redeploy transfers only what actually changed.

Coolify redeploy is needed **only** when `docker-compose.yaml` changes, or to restart
nginx after `nginx.conf` changed (the script says so when it does) — never for a new
game build.

## Brotli

`ProjectSettings` has `webGLCompressionFormat: 0` (Brotli) and
`webGLDecompressionFallback: 0`, so every payload is a pre-compressed `.br` file and
nginx must send `Content-Encoding: br` itself — that is what the `location ~* \.br$`
rules in `nginx.conf` do. If you ever switch the compression format in Unity, update
those rules or the loader will fail with "Unable to parse …".
