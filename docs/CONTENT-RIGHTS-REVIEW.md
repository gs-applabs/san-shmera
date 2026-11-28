# Content rights review — Χρονικολόγιο

## Scope and distribution decision

The MIT licence covers application code, not third-party data or images.
Content metadata inventory is not a legal clearance. Do not publish a new public
installer as rights-cleared until outstanding material has documented permission,
a sound public-domain basis, or has been excluded from that distributable build.
Keep original source data and evidence intact; do not silently delete it.

Last measured inventory on commit b1fcc1f9a3422d0cb4944fb3e59d5efe4b6bb6e4:
3,267 images, all matched to TSV/JSON declarations; 736 unknown-creator
declarations require review. The Synaxarion-derived list has 3,450 rows, with
redistribution permission not established. These are review counts, not counts
of infringements. An unknown author is not by itself evidence of infringement.

## Reproducible review queue

Run from the repository root:

    python tools/test_audit_content_rights.py
    python tools/audit_content_rights.py --output audit-metrics/content-rights.json

The CI report includes an image review queue grouped by exact source URL and
every associated local path. Missing/incomplete metadata and attribution-required
licences with an unknown creator have high priority. All entries remain pending
until evidence is independently reviewed.

For each source record:
- inspect the original file page, not just a thumbnail or search snippet;
- record the source URL, date of review, original work author and publication
  date if known, uploader (not necessarily author), stated licence and rights basis;
- consider the original work and any separate reproduction/photograph rights,
  the applicable jurisdiction and relevant terms of reuse;
- verify the stored image actually corresponds to the original and retain credit
  and licence obligations (including format conversion/cropping where appropriate);
- decide retain, replace with documented alternative, or exclude from public
  distribution. Pending or ambiguous evidence is not approval.

Review the selection/database rights and textual material separately from dates
and names. The Synaxarion site's own permission for reproducing church publications
does not establish redistribution permission for this app. Obtain explicit
permission or rebuild a documented independently curated list from appropriate
sources before treating that dataset as cleared.

## Development boundaries

Development and CI can continue without claiming legal clearance. Do not
change the current app theme or migrate WinForms to WPF until requested.
The rights queue is read-only and does not change library records or installer
contents. This is a technical review workflow, not legal advice.
