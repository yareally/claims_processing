# Claim Submission Practice

A timed practice run for an assessment that asks you to:

- build a claim-submission workflow in .NET that coordinates several services and undoes completed steps when a later step fails
- explain how pull requests are held to CI checks before they merge
- talk through your solution aloud

The assessment itself will differ. This project copies its shape from the invitation text only.

## Set up before the timed run

The project and the .NET 10 SDK are already on this PC, inside WSL (the Linux system that runs under Windows).

1. If VS Code is not installed yet, install [VS Code](https://code.visualstudio.com/) on Windows. Add its **WSL** and **C# Dev Kit** extensions.
2. Open a WSL terminal: in Windows Terminal, type `wsl`. Then run these commands:

   ```
   cd ~/idea_projects/claims_workflow_practice
   git switch -c my-attempt
   dotnet test
   code .
   ```

   You should see 8 tests pass and 31 fail. Those 31 are your work. The last command opens the project in VS Code.

The `solution` branch holds the answers. Stay off it until you finish.

## The timed run (55 minutes, 60 at most)

Use the terminal and an editor such as VS Code. You can use an AI assistant throughout: read `docs/04_ai_usage.md` first.

| Minutes | Stage | What to do |
|---|---|---|
| 5 | 1. Get familiar | Read `docs/01_brief.md`. Find the two TODOs. Run the tests. Send your AI tool one test question. |
| 20 | 2. Level 1 | The workflow's normal path, compensation (undoing completed steps), and status codes. |
| 15 | 2. Level 2 | Failures during compensation, cancelled requests, and failed emails. |
| 5 | 2. Level 3 | Duplicate requests. |
| 5 | 2. CI question | `docs/02_ci_question.md`. |
| 5 | 3. Walkthrough | Record yourself answering `docs/03_walkthrough.md`. |

Say your reasoning aloud as you work. The assessment asks for this, and it takes practice.

## Commands

Run the tests for one level:

```
dotnet test --filter Level1
dotnet test --filter Level2
dotnet test --filter Level3
dotnet test --filter EndToEnd
```

To see the whole system run, open two terminals:

```
dotnet run --project src/Downstream.Stubs --urls http://localhost:5200
dotnet run --project src/Claims.Api --urls http://localhost:5100
```

Then send the requests in `demo.http`. The policy number in the request decides which failure happens. Watch the log output in the `Claims.Api` terminal.

## After the timed run

1. Commit your work, then add the extra tests. These cover cases the starter tests leave out:

   ```
   git add -A
   git commit -m "My attempt"
   git checkout solution -- tests/Claims.Tests/Hidden
   dotnet test --filter Hidden
   ```

2. Compare your code with the reference solution:

   ```
   git diff my-attempt solution -- src/
   ```

3. Do the code review drill in `docs/05_review_drill.md`. It takes 10 minutes and practises finding bugs in AI-written code.

4. Read the answers in `docs/answers/` on the `solution` branch:

   ```
   git switch solution
   ```

To try again from a clean start, run `git switch main`, then `git switch -c attempt-2`.

## What is in this repo

| Path | What it is |
|---|---|
| `src/Claims.Api` | The API you finish. Your two TODOs are in `Workflow/ClaimSubmissionWorkflow.cs` and `ClaimEndpoints.cs`. |
| `src/Downstream.Stubs` | One small app that stands in for the five services the workflow calls. |
| `tests/Claims.Tests` | The tests, named by level. |
| `docs/` | The brief, the CI question, the walkthrough, AI tips, and the review drill. |
