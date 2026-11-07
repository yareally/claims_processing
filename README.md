| 5 | 2. Level 3 | Duplicate requests. || 15 | 2. Level 2 | Failures during compensation, cancelled requests, and failed emails. || 20 | 2. Level 1 | The workflow's normal path, compensation (undoing completed steps), and status codes. |# Claim Submission Practice

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

   All 27 tests fail at the start, and `dotnet test` prints only the first failure. Fix it, run again, and repeat. The last command opens the project in VS Code.

The `solution` branch holds the answers. Stay off it until you finish.

## Practise under the same limits

The Ropes editor is a limited online version of VS Code. Use no AI tools during a timed run.

Plan three runs:

| Run | Time limit | What you may use | Goal |
|---|---|---|---|
| 1 | None | C# extensions, the hints in the brief, `docs/06_syntax_sheet.md` | Learn the design |
| 2 | 55 minutes | `docs/06_syntax_sheet.md`, C# extensions off | Find what slows you down |
| 3 | 55 minutes | Nothing extra, C# extensions off | Match the assessment |

To turn the C# extensions off, open the Extensions panel in VS Code. On **C# Dev Kit** and **C#**, open the gear menu and choose **Disable (Workspace)**. Then run the tests in the terminal, because the assessment may not have autocomplete or a test panel.

## One timed run (55 minutes, 60 at most)

The minutes are estimates. Adjust them after your first timed run.

| Minutes | Stage | What to do |
|---|---|---|
| 5 | 1. Get familiar | Read `docs/01_brief.md`. Find the two TODOs. Run the tests. Answer the design questions in `docs/06_syntax_sheet.md` aloud. |
| 20 | 2. Level 1 | The workflow's normal path, compensation (undoing completed steps), and status codes. |
| 15 | 2. Level 2 | Failures during compensation, cancelled requests, and failed emails. |
| 5 | 2. Level 3 | Duplicate requests. |
| 5 | 2. CI question | `docs/02_ci_question.md`. |
| 5 | 3. Walkthrough | Record yourself answering `docs/03_walkthrough.md`. |

Say your reasoning aloud as you work. The assessment asks for this, and it takes practice.

If time runs short, finish Level 1 first. A working Level 1 is worth more than a half-finished Level 3. Then say aloud what you would do next.

## Commands

Run the tests for one level:

```
dotnet test --filter Level1
dotnet test --filter Level2
dotnet test --filter Level3
```

Each run stops at the first failure. To see every failure, run `dotnet test -- xUnit.StopOnFail=false`.

### How to read a failure

```
Failed SubmitAsync_when_case_step_fails_releases_the_reserve
Expected: Compensated
Actual:   InProgress
  at ...Level1CoreWorkflowTests.cs:line 88
```

- The test name starts with the method to fix: `SubmitAsync` or `ToHttpResult`.
- `Expected` and `Actual` show what the check found. The `line` is the check in the test.
- If your code threw an exception, the first stack trace line names your file and line.

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

3. Do the code review drill in `docs/05_review_drill.md`. It takes 10 minutes and practises finding bugs in someone else's code.

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
| `tests/Claims.Tests` | The tests, one class per level. |
| `docs/` | The brief, the CI question, the walkthrough, the review drill, and the syntax sheet. |
