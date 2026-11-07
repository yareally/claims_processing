# Answer: the CI question

## A one-minute spoken answer

> A CI workflow reports a result, but by itself it does not stop anyone from merging. So I need two parts.
>
> First, a workflow that runs on every pull request to `main`. For this project it restores packages, checks formatting, builds with warnings as errors, runs the tests, and checks packages for known vulnerabilities.
>
> Second, a branch protection rule or ruleset on `main`. It requires a pull request, lists that job as a required status check, and requires an approving review. GitHub then blocks the merge until the check passes.
>
> I would also require the branch to be up to date with `main`, or use a merge queue, so the tests run against the code as it will be after the merge. And I would apply the rule to admins, so nobody can bypass it by accident.
>
> AI-written code goes through the same checks, and a person still reviews it.

## The answer to each question in the prompt

| Question | Answer |
|---|---|
| What starts the checks? | The `pull_request` trigger in `.github/workflows/ci.yml`. |
| What stops the merge? | The required status check in the branch protection rule. The workflow file alone does not block anything. |
| Who can get around it? | Admins can, unless you turn on "Do not allow bypassing the above settings". Also block force pushes to `main`. |
| What if `main` changes after the checks pass? | Two changes can each pass alone and still break together. Turn on "Require branches to be up to date before merging", or use a merge queue when the team is large. |
| What if the workflow never runs? | The required check stays pending and the pull request stays blocked. See the first trap below. |
| What about AI-written code? | It gets no exceptions. The checks catch mechanical problems. A reviewer checks the logic, and `CODEOWNERS` can require the right reviewer for sensitive files. |

## Traps worth naming

- **Path filters.** If a `paths` filter skips a required workflow, its check stays pending and blocks the merge. [GitHub documents this.](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks)
- **Check names.** The required check must match the job name. Renaming the job leaves the rule waiting for a check that never reports.
- **Plan limits.** On a free GitHub account, these rules work on public repos only. [Private repos need a paid plan.](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- **Pull requests from forks.** They do not receive repo secrets by default. Avoid `pull_request_target` for running their code, because it runs with more access.
- **Flaky tests.** A test that fails at random teaches people to ignore or bypass the check. Fix it or remove it.

## If the team is not on GitHub

| Platform | Where the rule lives |
|---|---|
| Azure DevOps | A branch policy on `main`, with "Build validation" and "Require a minimum number of reviewers" |
| GitLab | Merge request settings: "Pipelines must succeed" |
| AWS CodeBuild with GitHub | CodeBuild runs the build and reports its status to GitHub. The required check is still set on GitHub. |

The idea is the same on each platform: the build runs automatically, and a server-side rule blocks the merge until it passes.
