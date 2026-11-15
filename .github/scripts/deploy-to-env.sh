#!/usr/bin/env bash
# Records a deploy on an environment branch that Argo CD follows (env/kind, env/azure), as one "Deployed ..." commit
# whose files are exactly this commit of main plus <values file>, the image to run. So the chart and the image move
# together, and nothing on the branch outlives a deploy: a hand edit there (an emergency fix) holds only until the
# next deploy replaces it. Roll back by reverting the change on main.
#
# The commit's parents are the branch's previous tip and the commit of main, so the branch's history is the deploy
# history and still shows which commit of main each deploy was. Nothing is merged, so nothing can conflict. Run by
# the Publish workflow, from a checkout with all history (it needs the commit of main, and pushes with its token).
#
#   deploy-to-env.sh <branch> <values file> <image repository> <environment>
#
#   <branch>            the environment branch, e.g. env/kind
#   <values file>       where the image goes, relative to the repo root, e.g. deploy/argocd/kind-image.yaml
#   <image repository>  the image without its tag, e.g. ghcr.io/aliator/medapp-api
#   <environment>       its name in the file's comment and the commit message, e.g. kind
#
# GITHUB_SHA is the commit of main to deploy, and the image's tag.
set -euo pipefail

branch=${1:?usage: deploy-to-env.sh <branch> <values file> <image repository> <environment>}
values_file=${2:?usage: deploy-to-env.sh <branch> <values file> <image repository> <environment>}
repository=${3:?usage: deploy-to-env.sh <branch> <values file> <image repository> <environment>}
environment=${4:?usage: deploy-to-env.sh <branch> <values file> <image repository> <environment>}
commit=$(git rev-parse --verify "${GITHUB_SHA:?GITHUB_SHA is the commit of main being deployed}^{commit}")

export GIT_AUTHOR_NAME="github-actions[bot]"
export GIT_AUTHOR_EMAIL="41898282+github-actions[bot]@users.noreply.github.com"
export GIT_COMMITTER_NAME=$GIT_AUTHOR_NAME
export GIT_COMMITTER_EMAIL=$GIT_AUTHOR_EMAIL

# The branch's current tip, if it exists yet.
parent=""
if git fetch --quiet origin "refs/heads/$branch" 2>/dev/null; then
  parent=$(git rev-parse --verify FETCH_HEAD)
fi

# The tree: main's, plus the values file. Built in a temporary index, so the checkout is never touched.
# (A new path, not an empty file, which git would reject as a broken index.)
scratch=$(mktemp -d)
trap 'rm -f "$scratch/index"; rmdir "$scratch"' EXIT
export GIT_INDEX_FILE=$scratch/index
git read-tree "$commit"
blob=$(git hash-object -w --stdin <<EOF
# The image the $environment environment runs: written by the Publish workflow, only on the $branch branch.
image:
  repository: $repository
  tag: $commit
  pullPolicy: IfNotPresent
EOF
)
git update-index --add --cacheinfo "100644,$blob,$values_file"
tree=$(git write-tree)

# A re-run for a commit already deployed has nothing to change.
if [[ -n $parent && $(git rev-parse "$parent^{tree}") == "$tree" ]]; then
  echo "$branch already runs $commit."
  exit 0
fi

if [[ -n $parent ]]; then
  parents=(-p "$parent" -p "$commit")
else
  parents=(-p "$commit")
fi
deploy=$(git commit-tree "$tree" "${parents[@]}" -m "Deployed medapp-api ${commit::7} to $environment")

# Not forced: if the branch moved since the fetch, the push fails rather than dropping that commit.
git push --quiet origin "$deploy:refs/heads/$branch"
echo "$branch now runs $commit ($deploy)."
