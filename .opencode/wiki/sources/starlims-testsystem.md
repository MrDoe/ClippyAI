---
title: STARLIMS 12 Testsystem — Application Overview
tags: [starlims, lims, biobank, bmbh, webapp, extjs, overview]
sourceRefs: [https://bmbh02.ads.krz.uni-heidelberg.de/starlims.test/starthtml.lims, C:/Daten/SLVSCODE/STARLIMS-Handbuch/Allgemeines/Arbeitsplatz - Startseite und Login.md]
lastReviewed: 2026-11-09
---

# STARLIMS 12 Testsystem (BioMaterialBank Heidelberg)

Live-exploration notes from 2026-11-09 (logged in as `DC`, Biobank NCT-CLB, Rolle Administrator; read-only). Part of the STARLIMS knowledge base together with the authoring workflow in the `starlims-manual-writer` skill.

## Access & login flow

- URL: `https://bmbh02.ads.krz.uni-heidelberg.de/starlims.test/starthtml.lims` (needs KRZ net/VPN). Page banner: „TESTSYSTEM – BioMaterialBank Heidelberg".
- Login card: Benutzername, Passwort, **Einloggen** (erst nach Benutzernamen aktiviert), **Benutzerkonto beantragen** (PDF in neuem Tab), **Hilfe**. Versteckt/hintergrundig vorhanden: „Mit SmartCard einloggen" (username + PIN dialog), Hilfe-Dialoge (Benutzername/Passwort vergessen, E-Mail-Code), Passwort-Änderungsdialog (current/new/confirmPassword).
- **Nach dem Login folgt ein Auswahl-Modal**: Biobank / Organisationseinheit / Benutzerrolle (vorausgefüllt: `NCT-CLB` / `BMBH` / `Administrator`; alle drei Pflicht-Comboboxes). Dieses Modal ist im Handbuch-Dokument „Arbeitsplatz – Startseite und Login" **nicht** beschrieben — Kandidat für ein Handbuch-Update.
- Testsystem-Hinweis: PID/BMBH-PID-Bereich ist mit Produktion geteilt — niemals echte Patientendaten ändern.

## Shell & Navigation

- Single-Page-Ext.NET-Shell (`starthtml.lims`); Modules open as **app-tabs** next to „Arbeitsplatz" (no URL change, iframe only for rich-text editor).
- Top bar shows Testsystem-Badge + current user; menu bar is personalized („Menü bearbeiten").
- Landing page „Arbeitsplatz": **LinkBar dashboard** with 6 groups → module links, plus widgets **Wichtige Mitteilungen** (announcements with rich-text editor) and **Abwesenheitskalender – BMBH-IT**.

### LinkBar groups (as rendered for DC/Administrator)

| Gruppe | Module/Links |
|---|---|
| Bioproben | Probenverwaltung, Probenrack-Verwaltung, RackScan |
| Klinische Daten | Fallverwaltung |
| Kommunikation / Support | Posteingang, Nachrichten senden, Analytics-Dashboard, Stargate-Aufträge |
| Biobankkonfiguration | Biobankverwaltung, Kollektivverwaltung, Metadatenvorlagen, Lagerverwaltung, Lagerstruktur-Vorlagen, Befüllungspläne, Wertelisten |
| Systemkonfiguration | Systemeinstellungen, Login-Verwaltung, Materialverwaltung, Einheitenverwaltung, Transaktionsverwaltung, Lagertemperaturen, Dokumente, Lizenzverwaltung |
| Entwicklung | Designer, API Konfiguration, API-Schlüssel, SSL-Referenz, Ticketsystem, Audit Trail, Import, KI-Konfiguration |

## Modul-Anatomie (Beispiel: Probenverwaltung)

- App-Tab with **11 Reiter**: Proben, Probenracks, Metadaten, Probendaten, Falldaten, Spenderdaten, Lagerung, Stammbaum, Kontrollkette, Probenentnahmen, Labware (→ each needs own screenshot+section per BMBH-IT "Tab-Struktur" rule).
- Proben grid columns (Auszug): `ORIGREC`, Status, Proben-ID, PID, STUDY_ID, CASE_ID, Empfänger, Inv. ID, Inventar-ID, Füllmenge, DISPLAY_AMOUNT_FT, Behältertyp, Mat. Code, Material, Zusatz, METADATA_1, Primärproben-ID, Primärprobe: Material/Zusatz, Standort, Probenrack-ID, Zeile, Spalte, Position, Reserviert für + Patientenspalten (Name, Vorname, Geburtsdatum, Geschlecht).
- Toolbar: Bearbeiten, filter bar with `StarlimsComboBox`/`StarlimsTextBox`/`StarlimsCheckBox` controls.

## UI-Technik & Automatisierung (verified)

- ExtJS/Ext.NET with STARLIMS custom components (`StarlimsLinkBarGroup-NNNN`, `StarlimsLinkBarItem-NNNN`, `StarlimsComboBox`, `StarlimsRichTextBox`); login uses anonymous ids (`textfield-1020-inputEl`, `button-1023`).
- Programmatic login works: set input values via native setter + `input`/`change` events → `Ext.getCmp('textfield-1020').getValue()` reflects them; buttons need a dispatched pointer sequence (`pointerdown→mousedown→pointerup→mouseup→click`), a bare `.click()` was ignored.
- LinkBar items: clickable node is the `StarlimsLinkBarItem-NNNN-targetEl` panel (icon+text spans inside), dispatch full pointer sequence there.
- firefox-devtools MCP `saveTo` only accepts its own output dir (`~/.firefox-devtools-mcp/output/`); use `saveTo:true` and the returned path.

## Credentials

Passwörter werden bewusst **nicht** hier gespeichert — Login über Auth-Vault/profil oder interaktiv; `DC`-Zugangsdaten siehe VS Code / ClippyAI Configuration (user-managed).

## Related

- `starlims-manual-writer` skill — authoring rules for the Handbuch (BMBH-IT requirements)
- `.opencode/wiki/log.md` — exploration timeline
