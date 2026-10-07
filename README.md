# DemoMAFHarness

DemoMAFHarness is a console demonstration of Microsoft Agent Framework (MAF). It uses
investment research to show the progression from a direct model call to an interactive
agent harness with planning, research tools, task tracking, and coordinated background agents.

The project illustrates how a harness supports the work around an AI model: gathering
evidence, managing a research workflow, involving the user in planning, and bringing
findings together into a report.

## Demonstrations

| Demonstration | What it shows |
| --- | --- |
| Direct Model Call | Answers a sample investment research question with access to web grounding. |
| Research Analyst Agent | Performs the sample research task through a dedicated analyst agent. |
| Research Harness — Execute Mode | Runs an interactive research workflow for a user-provided investment topic. |
| Research Harness — Plan Mode | Proposes a research plan for user review and approval before execution. |
| Research Harness — Background Agents | Delegates focused assignments to parallel research workers and combines their findings into one report. |

The first two demonstrations use a predefined question. The three harness demonstrations
support ongoing conversation and user-selected research topics.

## Harness capabilities

- **Web research:** discovers relevant sources through WebIQ and can inspect original
  source pages for additional context and verification. In the background-agent
  demonstration, workers perform this research on behalf of the coordinator.
- **Planning and approval:** in plan mode, supports clarification questions, review of
  proposed plans, and a transition to execution after approval.
- **Task tracking:** maintains a research task list and shows progress during the workflow.
- **Parallel research:** the background-agent demonstration divides broader questions
  into focused assignments, gathers worker findings, and synthesizes them into a coherent
  report with source links.
- **Report memory:** can save completed reports for continued use during the research workflow.
- **Session management:** supports starting fresh conversations and exporting or importing
  conversation sessions.
- **Visible activity:** streams responses and displays tool activity, agent status, and
  token usage in the console.

All five demonstrations also record telemetry to help inspect model and tool activity.

## Research output

The research instructions emphasize credible evidence, source freshness, and investment
relevance. Agents are instructed to cite major claims, distinguish facts from market
consensus and analysis, explain conflicting findings, and disclose uncertainty or missing
evidence. Research can address current conditions or an explicitly requested historical cutoff.

Completed reports are intended to follow six sections:

1. Executive Summary
2. Key Market Drivers
3. Investment Implications
4. Risks & Counterarguments
5. What to Watch Next
6. Sources

Harness agents are instructed to return the completed report to the user and save the
same report in memory. In the background-agent demonstration, the coordinator combines
worker evidence into the final report, covering the requested topics and comparisons.

## Example research questions

The sample question explores AI infrastructure across several industries:

> Analyze AI infrastructure spending and its investment implications for semiconductor companies, cloud providers, and power suppliers over the next 12–24 months.

Additional questions apply the harness's investment research perspective to other domains:

| Area | Example research question |
| --- | --- |
| Risk | What credit, liquidity, and refinancing risks could affect U.S. regional banks with commercial real estate exposure over the next 12 months, and which indicators would signal deterioration? |
| Legal and regulation | Which current and proposed AI regulations in the U.S. and EU could materially affect enterprise software companies over the next 24 months? Distinguish binding requirements from proposals and assess potential costs and legal uncertainties. |
| Finance | Compare Microsoft, Alphabet, and Amazon using their latest reported revenue growth, free cash flow, debt, and capital spending. Which assumptions most influence their valuations, and how do reporting periods differ? |
| AI | What evidence supports or challenges the business case for AI agents in enterprise software over the next 12–24 months? Compare adoption, customer returns, inference costs, and implications for software company margins. |
| Cybersecurity | How could ransomware and third-party security failures affect U.S. healthcare providers and cyber insurers over the next 12 months? Evaluate financial exposure, mitigation costs, and indicators investors should monitor. |
| Energy | Could U.S. electricity generation and grid capacity constrain data center expansion over the next three years? Compare the implications for utilities, power equipment suppliers, and cloud providers. |
| Supply chains | Assess how semiconductor supply concentration and export restrictions could affect chip designers, foundries, and equipment suppliers over the next 24 months. Compare disruption scenarios and potential mitigations. |
| Healthcare | Compare the commercial outlook for obesity treatments in the U.S. over the next three years, considering clinical evidence, competition, reimbursement, manufacturing capacity, and implications for drugmakers and health insurers. |

These questions invite evidence-based comparisons of opportunities, risks, and signals
to monitor, with source links and explicit discussion of unresolved questions.
