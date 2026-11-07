# Using AI during the run

The invitation lists "AI Usage Quality" as a tested skill. It does not say how it is scored, so read the assessment's own instructions on the day and follow them. The advice below is general practice for working well with an AI tool.

## What to do

- **Plan first, then ask.** Decide the design yourself, then give the AI one small task at a time.
- **Give it the rules.** Paste the relevant acceptance criteria and method signatures into your prompt.
- **Read every line before you accept it.** If you cannot explain a line, ask about it or rewrite it.
- **Run the tests after each change.** Passing tests are evidence. The AI saying the code works is not.
- **Ask it to attack the code.** For example: "What happens to this code if the caller cancels during the third step?" Then check the answer yourself.
- **Say out loud what you changed and why.** "It passed the caller's token to the undo call, so I changed that, because a disconnect would leave the money reserved."

## What to avoid

- Pasting the whole brief and accepting the whole answer.
- Asking "fix this" again and again without reading the error.
- Accepting tests that only repeat what the code already does.
- Pasting secrets or personal data into a prompt.

## Good and weak prompts

Weak:

> Implement the claim workflow.

Better:

> In this C# method, after `CreateReserveAsync` succeeds I want to record how to undo it, so that a later failure can release the reserve. The undo must not use the caller's cancellation token. Show me only the lines to add, and explain why you chose that data structure.

## Keep a short log

During the practice run, write down three things. They give you examples for the walkthrough.

| What I asked | What it gave me | What I kept, changed, or rejected, and why |
|---|---|---|
| | | |
| | | |
| | | |
