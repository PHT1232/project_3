# Superseded — see [/CLAUDE.md](../CLAUDE.md)

This file was written **before** `docs/Diagrams/` and `docs/Wireframe/` were added to the
repository, so several of its statements are now factually wrong:

- it says the ERD is "not in this repository" — `docs/Diagrams/ERD_project.png` is here, with
  all 12 tables;
- it says the AI feature is "NOT SPECIFIED" — DFD L2 8.0 specifies an AI Inventory Assistant
  in full (and a second AI Request Assistant appears in `docs/Wireframe/Request.png`);
- it says the authentication mechanism is "NOT SPECIFIED" — DFD L1 names JWT;
- it says inventory, supplier and product-management pages are "not in this project" — they
  appear in the ERD, DFD L2 2.0 and three wireframes;
- it cites documents that are not in this repository (`Phân_chia_công_việc.xlsx`,
  `Stationery_Management_System_Roadmap.md`, `Startup_Product_Kickoff.pdf`,
  `cau_truc_du_an.md`, `cau_hinh_funnel_tailscale.md`).

**Canonical project memory is now [`/CLAUDE.md`](../CLAUDE.md)** at the repository root, with
detail in [`docs/development/`](development/). The team-wide AI development standard remains
at [`docs/AI_INSTRUCTIONS.md`](AI_INSTRUCTIONS.md).

The original text is not preserved here; it is superseded rather than corrected, because
keeping two project-memory files in a five-person repository guarantees that someone follows
the wrong one.
