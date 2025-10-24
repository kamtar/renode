# GitHub Actions Workflows

This directory contains GitHub Actions workflows for maintaining this forked repository.

## Workflows

### 1. Sync Fork with Upstream (`sync-fork.yml`)

This workflow automatically syncs the `kamtar/renode` fork with its upstream repository (`renode/renode` or `antmicro/renode`).

**Features:**
- Runs daily at 00:00 UTC (can be triggered manually)
- Syncs with upstream using "ours" merge strategy for conflict resolution
- Triggers the sync workflow in `kamtar/renode-infrastructure`
- Updates the `renode-infrastructure` submodule to the latest commit from the `kamtar` fork
- Automatically commits and pushes changes

**Manual Trigger:**
Go to Actions → Sync Fork with Upstream → Run workflow

### 2. Sync Infrastructure Fork with Upstream (`sync-infrastructure-fork.yml`)

This workflow is designed to be copied to the `kamtar/renode-infrastructure` repository. It syncs the infrastructure fork with its upstream.

**Features:**
- Runs daily at 00:00 UTC (can be triggered manually or via repository_dispatch)
- Syncs with upstream using "ours" merge strategy for conflict resolution
- Automatically commits and pushes changes

**To deploy to kamtar/renode-infrastructure:**
1. Copy `.github/workflows/sync-infrastructure-fork.yml` to the `kamtar/renode-infrastructure` repository
2. Rename it to `sync-fork.yml` in that repository
3. Commit and push

## Configuration

### Personal Access Token (Optional)

To enable cross-repository workflow triggering, you may need to create a Personal Access Token (PAT):

1. Go to GitHub Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Generate a new token with the following scopes:
   - `repo` (Full control of private repositories)
   - `workflow` (Update GitHub Action workflows)
3. Add the token as a repository secret:
   - Go to Repository Settings → Secrets and variables → Actions
   - Create a new secret named `PAT_TOKEN`
   - Paste your token as the value

**Note:** If `PAT_TOKEN` is not configured, the workflow will use `GITHUB_TOKEN`, which has limited cross-repository permissions.

## Conflict Resolution Strategy

Both workflows use the "ours" merge strategy (`-X ours`) to resolve conflicts. This means:
- In case of conflicts, changes from the current repository (kamtar fork) take precedence
- Changes from upstream are merged where they don't conflict
- This ensures local modifications are preserved

## Workflow Execution Order

1. **kamtar/renode-infrastructure sync workflow** runs (either scheduled or triggered)
   - Syncs with upstream `renode-infrastructure` or `antmicro/renode-infrastructure`
   - Resolves conflicts using "ours" strategy
   - Pushes changes

2. **kamtar/renode sync workflow** runs
   - Syncs with upstream `renode/renode` or `antmicro/renode`
   - Triggers the infrastructure sync (step 1)
   - Waits for infrastructure sync to complete
   - Updates the `src/Infrastructure` submodule to latest commit from `kamtar/renode-infrastructure`
   - Pushes all changes

## Troubleshooting

### Workflow not triggering infrastructure sync

If the main workflow cannot trigger the infrastructure sync:
1. Ensure `sync-fork.yml` is deployed in `kamtar/renode-infrastructure`
2. Check if `PAT_TOKEN` is configured (see Configuration section)
3. Manually trigger the infrastructure sync workflow
4. The main workflow will continue even if triggering fails

### Submodule update fails

If the submodule update step fails:
1. Check that `kamtar/renode-infrastructure` repository exists and is accessible
2. Verify the branch name (should be `master` or `main`)
3. Manually update the submodule:
   ```bash
   cd src/Infrastructure
   git fetch origin
   git checkout origin/master
   cd ../..
   git add src/Infrastructure
   git commit -m "Update renode-infrastructure submodule"
   git push
   ```

### Merge conflicts not resolved correctly

If the "ours" strategy doesn't work as expected:
1. Review the workflow logs to see which files had conflicts
2. Manually resolve conflicts if needed
3. Consider adjusting the merge strategy in the workflow file

## Branch Support

Both workflows support:
- `master` branch (default)
- `main` branch (fallback)

The workflows automatically detect which branch is used in the upstream repository.
