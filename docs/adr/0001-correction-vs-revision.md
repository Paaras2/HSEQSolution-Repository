# ADR 0001 — Metadata correction vs. document revision are two tiers, not two peers

- Status: Accepted
- Date: 2026-09-20
- Context: EDMS for controlled QMS/ISO documents (version letters A→B, review dates, HSE/ISO document types).

## Context

The UI and API separate "metadata editing" (`Document/Update`) from "document
revision" (`Document/Revise`). Reading the code shows the two edit almost the
same fields:

- `Update` edits `Name`, `FormerReviewDate`, `CurrentReviewDate`.
- `Revise` edits `Name?`, `FormerReviewDate?`, `CurrentReviewDate?` **plus a
  `File`**, spawns a new `Document`, and retires the old one.

Identity/coding fields (`Number`, `DocumentTypeId`, `Organizational*Id`,
`ProjectId`, `Category`, `SerialNumber`) are already locked after creation.

## Decision

Keep revision as a distinct, deliberate governance action; do not merge it into
a generic "edit". Model change by **governance weight**, in tiers:

1. **Identity / coding** — locked after creation. Changing = re-coding, a rare
   controlled action, not an everyday edit.
2. **Controlled correction** (`Update`) — in-place fix of the *current* version's
   descriptive fields (`Name`, review dates) with **no file change**. Audited.
   Editorial: does not bump the version letter.
3. **Revision** (`Revise`) — content/file change or a formal review outcome.
   Carries the metadata that changes *with* the revision, atomically; new
   version, old one retired. Major: bumps the version letter.

**Hard invariant:** a file/content change may occur **only** through `Revise`,
never through the correction path. (Enforced today by shape: `Update` has no
`File`; `Revise` does. Preserve this.)

**Contract:** treat `Revise` as `Update`'s metadata fields **plus** `File`, so the
two request models cannot drift apart in validation or shape.

## Consequences

- The governance boundary (correction vs. revision) is preserved — correct for an
  ISO 9001 §7.5 controlled register.
- The smell to fix is duplicated forms/DTOs over identical fields, not the
  boundary itself: unify the metadata contract.
- Corrections mutate a controlled record without a version bump, so they must be
  audited (who/when, ideally a reason). See Phase 2 audit-trail work.
- Free descriptive tags/keywords (not yet in the model) would be a **third**,
  lighter tier — inline, edit-anytime — not a companion to title/date corrections.
