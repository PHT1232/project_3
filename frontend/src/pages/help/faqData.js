/**
 * Static Help / Q&A content (Plan T6.1 [SPEC]: "≥15 questions covering every feature").
 *
 * Kept as a frontend module for now — the Plan lists `GET /api/v1/help/faq` as the eventual
 * source, so this is shaped as a flat list the page could later fetch instead with no other
 * change. Edit here via a PR; there is no admin screen by design.
 *
 * Every §4.2 feature area has at least one entry — see `FAQ_AREAS` for the checklist the
 * test asserts against.
 */

export const FAQ_AREAS = [
  'Getting started',
  'Requests',
  'Tracking your requests',
  'Approvals',
  'Budget & eligibility',
  'Notifications',
  'Reports',
  'Account',
]

export const faqEntries = [
  // --- Getting started -----------------------------------------------------------------
  {
    area: 'Getting started',
    question: 'How do I sign in?',
    answer:
      'Use your employee number and password on the login page. There is no self sign-up — a Manager or above creates your account. If you have never logged in, ask your manager for your starting password and change it from Account settings afterwards.',
  },
  {
    area: 'Getting started',
    question: 'Why can I see fewer menu items than a colleague?',
    answer:
      'The sidebar shows what your role can use. Inventory, Suppliers, Item Management and User Management are Manager-and-above. Everyone sees Dashboard, Catalogue, My Requests, Approvals, Reports and Help.',
  },
  {
    area: 'Getting started',
    question: 'What do the roles mean?',
    answer:
      'Engineer, Manager, Business Manager and Managing Director form a reporting line. Your approver is your listed superior. Managers and above also get the cost reports and master-data screens.',
  },
  {
    area: 'Getting started',
    question: 'What is on the Dashboard?',
    answer:
      'A snapshot: how many requests are waiting on your approval, low-stock alerts (Manager+), your remaining monthly budget, and a Recent Requests table you can filter by this week, this month or a custom date range.',
  },

  // --- Requests ------------------------------------------------------------------------
  {
    area: 'Requests',
    question: 'How do I raise a stationery request?',
    answer:
      'Open Catalogue, add the items and quantities you need, then go to New Request to review the lines, optionally set a required-by date, and submit. One request can contain many item lines.',
  },
  {
    area: 'Requests',
    question: 'Why are some catalogue items not available to me?',
    answer:
      'Each item has a minimum rank to request it. If your role is below that rank the item is hidden or shown as unavailable. Ask your manager to raise the request on your behalf if you genuinely need it.',
  },
  {
    area: 'Requests',
    question: 'Who approves my request?',
    answer:
      'Your superior in the reporting hierarchy, filled in automatically. You cannot pick a different approver — that is deliberate, so requests cannot be routed around the line manager.',
  },
  {
    area: 'Requests',
    question: 'What is the AI assistant on the New Request page?',
    answer:
      'Type what you need in plain language ("2 black pens and a stapler for the lab") and it drafts the matching item lines for you to review. It never submits anything by itself, and it falls back to a keyword match if the AI service is unavailable.',
  },
  {
    area: 'Requests',
    question: 'Can I edit a request after creating it?',
    answer:
      'You can edit it while it is still a draft or after an approver returns it for modification. Once it is pending approval you would withdraw it and raise a new one.',
  },

  // --- Tracking your requests --------------------------------------------------------
  {
    area: 'Tracking your requests',
    question: 'Where do I see the status of my requests?',
    answer:
      'My Requests lists everything you have raised with its current status and full history. The Dashboard’s Recent Requests table is a shorter view of the same data.',
  },
  {
    area: 'Tracking your requests',
    question: 'What do the statuses mean?',
    answer:
      'Pending: waiting on your approver. Approved / Partially Approved: granted in full or in part. Rejected or Returned: sent back with a comment. Withdrawn: you pulled it back. Cancellation Pending / Cancelled: a two-step cancellation of an approved request. Approving a request is also what issues the stock.',
  },
  {
    area: 'Tracking your requests',
    question: 'How do I withdraw a request?',
    answer:
      'Open the request from My Requests and choose Withdraw. This is only possible while it is still Pending. It frees any budget the request was holding.',
  },
  {
    area: 'Tracking your requests',
    question: 'How do I cancel a request that was already approved?',
    answer:
      'Request cancellation from the request page. That moves it to Cancellation Pending; your approver then confirms or refuses. This two-step flow exists because approved requests may already have stock committed.',
  },

  // --- Approvals ---------------------------------------------------------------------
  {
    area: 'Approvals',
    question: 'Where is my approvals queue?',
    answer:
      'The Approvals page, or the "Review approvals" button on the Dashboard’s Pending Approvals card. It lists requests from your team that are waiting on you.',
  },
  {
    area: 'Approvals',
    question: 'What are my options on a request?',
    answer:
      'Approve (in full or per line), Reject, or Return for modification. Reject and Return both require a comment that the requestor will see.',
  },
  {
    area: 'Approvals',
    question: 'Which requests can I see as a manager?',
    answer:
      'Your own, any waiting on your approval, and every request raised by someone in your reporting line beneath you — your reports and their reports. You do not see a peer manager’s team or anyone above you. The Managing Director sees everything.',
  },
  {
    area: 'Approvals',
    question: 'Can I act on another manager’s team’s request?',
    answer:
      'No. Approval is limited to your own reporting line. A request outside it will not appear in your queue and opening its link returns "not found".',
  },

  // --- Budget & eligibility --------------------------------------------------------
  {
    area: 'Budget & eligibility',
    question: 'What is the "Remaining Budget" figure on my Dashboard?',
    answer:
      'Your monthly spending allowance for the role you hold, minus what you have already committed this calendar month. Engineer 500, Manager 2,000, Business Manager 5,000, Managing Director 20,000 per month.',
  },
  {
    area: 'Budget & eligibility',
    question: 'What spending counts against my budget?',
    answer:
      'Requests you raised this month whose status is not Rejected, Withdrawn or Cancelled, valued at their estimated cost. Withdrawing or having a request rejected returns that amount to your remaining budget.',
  },
  {
    area: 'Budget & eligibility',
    question: 'When does my budget reset?',
    answer:
      'On the first day of the next month (UTC). The Dashboard tile shows the exact reset date.',
  },
  {
    area: 'Budget & eligibility',
    question: 'The budget tile says "Not available" — why?',
    answer:
      'The Dashboard could not reach the eligibility service on that load. The rest of the Dashboard still works; refresh the page and it usually returns.',
  },

  // --- Notifications --------------------------------------------------------------
  {
    area: 'Notifications',
    question: 'What triggers a notification?',
    answer:
      'Six events: a request submitted to you, approved, rejected, returned, a cancellation requested, and a cancellation decided. Both the requestor and the approver are notified as relevant.',
  },
  {
    area: 'Notifications',
    question: 'Where do notifications appear?',
    answer:
      'The bell in the top bar shows an unread count and opens the feed. It is in-app only — the system does not send email or SMS.',
  },

  // --- Reports -----------------------------------------------------------------------
  {
    area: 'Reports',
    question: 'What reports are available?',
    answer:
      'Cost by item, cost by month, and cost by manager/team. Open the Reports page and pick a date range.',
  },
  {
    area: 'Reports',
    question: 'Why do my report totals look smaller than a manager’s?',
    answer:
      'Reports are scoped to your place in the reporting line, server-side. A requestor sees only their own spend; a manager sees their team; the Managing Director sees the whole organisation.',
  },

  // --- Account ---------------------------------------------------------------------
  {
    area: 'Account',
    question: 'How do I change my password?',
    answer:
      'Open the account menu in the top-right and choose Change password. You need your current password to set a new one.',
  },
  {
    area: 'Account',
    question: 'I am locked out or my account seems inactive.',
    answer:
      'Five wrong passwords locks the account for 15 minutes. If it is deactivated entirely, a Manager or above has to reactivate it from User Management — contact your manager or the team via the button below.',
  },
]
