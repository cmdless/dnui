# Specification Quality Checklist: Windows and Built-in Dialogs

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-11
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Rewritten 2026-11-11 after design discussion. There's no shared host: host commands versus window
  commands (constitution 2.3.0), one-shot windows with no protocol, and only the built-in pickers
  (open file, open folder, save file). The message box and other kinds are deferred.
- As in spec 001, the audience is developers, and the product is a CLI contract plus a library.
  So XAML windows, JSON result lines, and option names count as contract, not implementation
  detail. Exact option spelling is left to the plan's CLI contract.
- No clarifications were needed. Defaults are recorded in Assumptions and Edge Cases:
  - a missing start folder falls back to the platform default
  - results are always lists for the open dialogs
  - parented dialogs are queued
  - child windows must be types already loaded in the window's process
- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
