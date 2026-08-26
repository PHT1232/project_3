# AI Usage Reporting Standard

You are an AI coding assistant working as part of a five-member student software development team.

The project requires mandatory AI usage reporting.

All AI coding agents used by the team must follow this same reporting standard.

The project may use multiple AI tools, including but not limited to:

- Claude
- Claude Code
- Cursor
- GitHub Copilot
- Gemini
- Gemini CLI
- Cline
- Other AI coding agents

Your task is to record your meaningful contribution to:

docs/AI-Usage-Report.md

==================================================
1. WHEN TO RECORD AI USAGE
==================================================

Record an entry when you meaningfully contribute to:

- Requirement analysis
- System design
- Database design
- API design
- UI implementation
- Backend implementation
- Frontend implementation
- AI feature implementation
- Testing
- Debugging
- Refactoring
- Documentation
- Code review
- Architecture decisions

Do NOT record trivial interactions.

==================================================
2. DO NOT INVENT INFORMATION
==================================================

This is a mandatory rule.

Never fabricate:

- Prompts
- Dates
- Results
- Screenshots
- Test results
- AI contributions
- Developer actions

Only report information that can be verified from the current AI interaction, project files, commands, test results, Git changes, or information explicitly provided by the developer.

If information is unavailable, write:

"Not recorded"

instead of guessing.

==================================================
3. REPORT FORMAT
==================================================

The report must contain this table:

| Date | AI Tool | Prompt / Purpose | Result & Developer Adjustment |
|------|---------|------------------|------------------------------|

Keep each entry concise.

Do NOT copy the entire prompt.

Summarize what the AI was asked to accomplish.

==================================================
4. RESULT & DEVELOPER ADJUSTMENT
==================================================

Always distinguish between:

AI contribution

and

Developer/team contribution.

Example:

"AI generated the Product API. The developer reviewed the validation logic, fixed two incorrect assumptions, and verified the API using Postman."

Do NOT write:

"AI completed the Product API."

unless that statement is genuinely accurate and verified.

==================================================
5. AI TOOL NAME
==================================================

Use the actual AI tool used.

Examples:

Claude
Claude Code
Cursor
GitHub Copilot
Gemini CLI
Cline

Do not use a generic name such as "AI" when the actual tool is known.

==================================================
6. SCREENSHOT EVIDENCE
==================================================

Do not create fake screenshots.

Use:

| ID | Screenshot | Purpose |
|----|------------|---------|

Examples:

S1 – Requirement analysis
S2 – System design
S3 – AI-assisted coding
S4 – AI-assisted testing
S5 – Debugging

If no screenshot exists, write:

"Screenshot not attached"

==================================================
7. PRESERVE EXISTING RECORDS
==================================================

Never delete previous AI usage records unless explicitly instructed.

Append new entries chronologically.

Do not create duplicate entries.

Do not rewrite historical entries simply because your wording is different.

==================================================
8. KEEP THE REPORT SIMPLE
==================================================

The report is intended for:

- Academic submission
- Human reviewers
- Project documentation

Keep entries short and readable.

One meaningful development activity should normally produce one report entry.

Do not record every small prompt.

==================================================
9. BEFORE FINISHING A TASK
==================================================

Ask yourself:

1. Did AI meaningfully contribute?
2. Can the contribution be verified?
3. Is the AI tool known?
4. Is the purpose clear?
5. Is the result accurately described?
6. Is the developer's review/adjustment recorded?
7. Did I avoid fabricating information?

If the task qualifies as meaningful AI usage, update:

docs/AI-Usage-Report.md

==================================================
10. TEAM-WIDE RULE
==================================================

All five team members use the same reporting format regardless of which AI coding agent they use.

The purpose of this file is to create one unified AI usage history for the entire project.

Do not create separate AI usage reports for different AI tools unless explicitly instructed.

==================================================
11. GIT
==================================================

Do not automatically commit the AI Usage Report unless explicitly requested.

The developer/team will decide when to commit and push the report.

==================================================
12. FINAL PRINCIPLE
==================================================

The report must answer four questions:

1. Which AI tool was used?
2. What was AI asked to do?
3. What did AI produce?
4. What did the developer/team verify, modify, or reject?

Accuracy is more important than making AI usage appear extensive.
