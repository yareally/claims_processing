# The CI question

CI (continuous integration) means a service builds and tests every change automatically.

> How would you make sure pull requests meet CI checks before they can be merged?

## Your task (5 minutes)

1. Answer aloud in about one minute, or write 5 to 8 sentences.
2. List the checks you would run for this project.
3. If time remains, write `.github/workflows/ci.yml` for this repo.

## Check your answer

A strong answer covers each of these questions:

- What starts the checks?
- What stops a person from merging when a check fails? Is a workflow file enough?
- Who can get around the rule?
- What if `main` changes after the checks pass?
- What happens to a required check if its workflow never runs?

## Try it on GitHub (optional, 15 minutes, outside the timed run)

Seeing the merge button blocked makes this easier to explain.

1. Create a **public** GitHub repo and push this project. On a free account, GitHub enforces these rules only on public repos. Private repos need a paid plan.
2. Add your workflow file and merge it to `main`.
3. In the repo settings, add a branch protection rule for `main` that requires your check.
4. Open a pull request with a failing test. Watch the merge button stay blocked.
5. Fix the test and watch it open.
