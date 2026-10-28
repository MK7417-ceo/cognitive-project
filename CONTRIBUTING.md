# Contributing to Cognitive Project

VR-based oculomotor and visuomotor training system for cricket players.
This file is the rulebook for our 3-member team. If you follow this file,
you will not break anyone else's work.

## Setup

- Unity version: 6000.4.4f1 exactly. Do not use any other version.
- Install Git, then run: git lfs install
- Clone the repo, then run: git lfs pull
- For the Python apps, run: pip install streamlit groq pandas python-dotenv
- Each system builds its own Library folder. Never copy another
  machine's Library folder.
- Each member creates their own .env file. Never share API keys.

## How Work Flows

1. Pick an issue from the project board and move it to Doing.
2. Update main first:
   git checkout main
   git pull
3. Create a branch for that issue:
   git checkout -b feature/<your-name>-<task>
   Example: feature/tarun-module1-reaction
4. Work in small commits, one task per commit.
5. Test in Play mode yourself before opening a pull request.
6. Push the branch and open a pull request.
   In the description, write what you did, how you tested it,
   and add Closes #<issue-number> for the issue it fixes.
7. Another member reviews the code and tests it in Play mode
   on their own system, then approves it.
8. The lead merges the pull request.
9. After merging, everyone updates main and deletes the old branch.

## Branch and Commit Rules

- No direct push to main. Every change goes through a pull request.
- Branch name: feature/<your-name>-<task>
- Commit messages must be clear:
  feat: add reaction time recording
  fix: fix bowler stall after over ends
- Always commit a script together with its .meta file.
  Never delete a .meta file.

## Unity Rules

- Only one person edits SampleScene at a time, after telling
  the group first. Unity scenes merge very badly.
- Put most work in scripts and prefabs, not in scene edits.
- Never commit Library/, Temp/, Logs/, or .env.
- Never upgrade the Unity version on one machine alone.
  Upgrade only together, after telling everyone.

## Pull Request Checklist

- [ ] Tested in Play mode
- [ ] Script committed together with its .meta file
- [ ] No Library/, Temp/, Logs/, or .env files committed
- [ ] Issue number linked in the description
- [ ] Screenshot added if the change affects the UI

## Security Rules

- Never put an API key, token, or .env file in the repo.
- Never paste a token in chat or in the group.
- Each member uses their own Groq API key in their own .env file.
