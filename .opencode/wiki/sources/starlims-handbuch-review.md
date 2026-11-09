---
title: STARLIMS-Handbuch-Review gegen Live-Testsystem (HC12.1)
tags: [starlims, handbuch, review, quality, bmbh, documentation]
sourceRefs: [C:/Daten/SLVSCODE/STARLIMS-Handbuch, https://bmbh02.ads.krz.uni-heidelberg.de/starlims.test/starthtml.lims, C:/Daten/Entwicklung/ClippyAI/.opencode/wiki/sources/starlims-testsystem.md]
lastReviewed: 2026-11-09
---

# STARLIMS-Handbuch-Review gegen Live-Testsystem (HC12.1)

Vollständige Durchsicht aller **48 Handbuch-Dokumente** (09.11.2026, Stand git `4f813de` + aktuelle HeiBox-Syncs) im Abgleich mit dem live Testsystem (Badge **HC12.1, Stand 17.09.2026**) — maßgebliche Referenz: Transaktionsverwaltung (37 aktivierte Transaktionen), LinkBar-Menü (32 Links), Modul-Reiter.

## 1. Dokumentstatus gesamt

| Kategorie | Anzahl |
|---|---|
| Unbearbeitete Vorlagen-Kopien (Platzhalter-H1, 44 Zeilen, nur Template-Bild) | **16** |
| Inhaltliche Docs mit Template-Restbeständen im Körper („Punkt 1 - Gliederung“, „Verfahren: [Überschrift 2…]“) | ~6 (u. a. Umlagern, Entnehmen, Kollektivverwaltung-Wurzel, Probenverwaltung_Prozess) |
| Vollständig inhaltlich + formalsauber | ~20 |
| Kopf-Tabelle mit Ersterstellungs-DATUM befüllt | nur **5/48** |
| Word-Import-Artefakte (`&#xA;`, `&#x20;`, `&nbsp;`) | 13 Docs (max: Probenlisten abgleichen, roXtra-Arbeitsplatz) |

**16 Stubs:** BMBH (Lagerverwaltung, Kollektiv ändern, Metadatenvorlage ändern, PatientenID ändern, Probenempfang, Probenrackverwaltung), DZIF (Füllmenge ändern, Listen verwalten), Kinderkardiologie (ProbenID ändern, Status ändern), Pancobank (RackScan, Befüllungsplan anwenden, Standort ändern Rack umlagern), VBBH (Entsorgen, Metadaten ändern), CLB-Archiv (cancelled Charge ändern).

## 2. Namens-/Zuordnungskonflikte mit dem Live-System (wichtigste Kategorie)

Die **Transaktionsverwaltung** (Systemkonfiguration) ist die maßgebliche Namensquelle. Gefundene Diskrepanzen:

| Dokument | Live-Wirklichkeit | Befund |
|---|---|---|
| Transaktion **Probenrackverwaltung** (BMBH) | KEINE Transaktion — es ist ein **Modul** „Probenrack-Verwaltung“ (Reiter: Probenracks, Allgemein, Lagerung, Kontrollkette) | Dokument falsch klassifiziert (u. Stub) |
| Transaktion **Listen verwalten** (DZIF) | Keine Transaktion dieses Namens; am nächsten: Modul **Wertelisten** bzw. Transaktion „Mit Liste abgleichen“ | Titel existiert nicht im System (Stub) |
| Transaktion **Umlagern** (Lungenbiobank) | Keine Transaktion nur „Umlagern“ — live: „Umlagern (Rack zu Rack)“ (ContainerToContainer) und „Standort ändern (ohne Rack-Position)“ (MoveInventory) | Titel existiert so nicht; Körper halb Template; mögliche Überschneidung mit „Umlagern (Rack zu Rack)“-Doc |
| Transaktion **Standort ändern Rack umlagern** (Pancobank) | Verschmilzt zwei live Transaktionen (MoveInventory + ContainerToContainer) | Aufteilen oder umbenennen (Stub) |
| Transaktion **ProbenID ändern** (Kinderkardiologie) | „**Proben-IDs ändern**“ (ChangeSampleIDs) | Titel korrigieren |
| Transaktion **PatientenID ändern** (BMBH) | „**Patient:innen-ID ändern**“ (SetPatientID) — Gendersprache im Live-UI | Titel/Doku-Terminologie anpassen |
| Transaktion **Metadatenvorlage ändern** (BMBH) | „**Metadaten-Vorlage ändern**“ (ChangeMetadataTemplate) | Bindestrich/Kleinschreibung |
| roXtra Transaktion **Probenlisten abgleichen** (CLB, freigegeben) | Alias live: „**Mit Liste abgleichen**“ (CompareToList) | Freigegebenes Dok mit abweichendem Alias — Klärung mit BMBH-IT |
| cancelled_**Charge ändern** (CLB-Archiv) | Live: „Labware-Charge ändern“ (ChangeLabware) | Archivierte Fassung — Namen für später merken |
| Transaktion **Probenempfang** (BMBH, Stub) | Zwei Transaktionen: „Neuer Probenempfang“ (AddReception,avail=A) + „Probenempfang schließen“ (CloseReception,avail=N) | Dok muss beide abdecken/scheiden |

Nicht aktive live Transaktionen (Verfügbar=N — bei Dokumentation als „derzeit deaktiviert“ kennzeichnen): Entnahme löschen, Anhänge verwalten, Einwilligungen verwalten, Probenempfang schließen.

## 3. Moduldokumente vs. Live-Reiter

- **Probenverwaltung (NCT, hoch, in Arbeit):** Live **11 Reiter** (Proben, Probenracks, Metadaten, Probendaten, Falldaten, Spenderdaten, Lagerung, Stammbaum, Kontrollkette, Probenentnahmen, Labware) — Dok behandelt nur 3 (Proben, Probenracks, Metadaten) → verletzt BMBH-IT-Regel „jeder Tab pro Screenshot dokumentiert“; **8 Reiter fehlen**.
- **Kollektivverwaltung (BMBH):** Wurzel-Dok gut (deckt Live-Reiter Probenracks/Details/Dokumente/Materialien/Benutzer inkl. Unterreiter Allgemeines/Zähler/ISH-Schnittstelle/Etikettendruck und Tabellen-/Baumansicht). **Achtung Duplikat-Falle:** Datei existiert zweimal (Wurzel alt/leere-Kopf + `Bearbeitete Dokumente\` neu mit Datum 09.11 + Metadaten-Hinweis) — die Neue ist maßgeblich, Alte ausmustern. ISH-Schnittstelle/Etikettendruck explizit ergänzen.
- **Metadatenvorlagen (BMBH, fertiggestellt):** Struktur deckt sich mit Live-Modul (Vorlagenverwaltung, Felder, Eigenschaften, Position/Größe, Vorschau, Wertelisten, Aufruf aus anderen Modulen) — ok; aber **zwei inhaltsgleiche Dateien** (`Modul Metadatenvorlagen…` + `roXtra - Modul Metadatenvorlagen…`, je L387/img17) → Redundanz klären.
- **Befüllungspläne (Lungenbiobank):** inhaltlich stark (480 Z., img47); Live-Existenz bestätigt.
- **Fallverwaltung (Lungenbiobank):** nur 90 Z., Unterstruktur aus Vorlage, **plus SFConflict-Duplikat** (09.11 15:12) — Duplikat löschen, Inhalt fehlt.
- **BMBH roXtra-Arbeitsplatz-Dok:** deckt Login + Rollen-Modal + Kachelmenü ab; deckungsgleich mit Live (LinkBar 6 Gruppen/32 Links identisch). §4.5 (Biobank/Org/Rolle-Modal) vorhanden — Handbuch-Hinweis in [[sources/starlims-testsystem]] damit hinfällig/als erledigt vermerken.

## 4. Pflichtinhalt Metadaten-Hinweis (BMBH-IT-Regel „Metadaten sind nach den Vorgaben der eigenen Biobank zu verwenden und zu pflegen“)

In Metadaten-relevanten Docs **fehlt** der Hinweis u. a. in: Kollektivverwaltung (Wurzel), Fallverwaltung (beide), Probenverwaltung-Dok, Entnehmen (beide), Proben registrieren, Metadatenvorlagen-Dokumente (trotz Modulname!), Aliquotieren, Probenzustand ändern. Vorhanden nur: Befüllungspläne, Kollektivverwaltung (neue Fassung).

## 5. Formales

- Kopf-Tabelle (Ersterstellung/Prüfung/Freigabe) fast überall leer → Freigabe-Workflow nicht nachvollziehbar.
- Typos: roXtra „Zum Ursprungsort verschieben“ → H1 „**Urspungsort**“; „**Aliqout**-Röhrchen“ (Umlagern-R2R).
- Trennstriche in Dateinamen uneinheitlich: „Modul RackScan- Priorität hoch“ (Leerzeichen nach Bindestrich fehlt) usw.
- HeiBox-Bildreferenzen: alle inhaltlichen Docs nutzen korrektes URL-Muster; lokale Bilddateien vorhanden.

## 6. Priorisierte Maßnahmen

1. **Prio 1 — Titel-Korrekturen gegen Transaktionsverwaltung** (Alias live ist verbindlich): Probenrackverwaltung→Modul-Dok; Umlagern/Standort ändern Rack umlagern→aufteilen; ProbenID→Proben-IDs; PatientenID→Patient:innen-ID; Metadatenvorlage→Metadaten-Vorlage; Listen verwalten→Value-list-Klärung; Probenlisten abgleichen↔„Mit Liste abgleichen“ klären.
2. **Prio 1 — Duplikate bereinigen:** Fallverwaltung (SFConflict), Kollektivverwaltung (Wurzel vs. Bearbeitete), Metadatenvorlagen (alt vs. roXtra), NCT „Ursprungsort ändern“ (3 Dateien), Entnehmen/Reservieren (je 2).
3. **Prio 2 — Probenverwaltung: 8 fehlende Reiter dokumentieren** (Regelverstoß in hoch-Priorität-Dok).
4. **Prio 2 — Metadaten-Pflichthinweis** in alle metadaten-relevanten Docs.
5. **Prio 3 — 16 Stubs** entweder füllen oder aus dem Verkehr ziehen; Kopf-Tabellen datieren; Word-Entities bereinigen.

## Methodik

Live-Abgleich via firefox-devtools MCP (Session DC/NCT-CLB/Administrator, read-only). Hinweis: Vollständige `body.innerText`-Abfragen auf großen DPs (z. B. Kollektivverwaltung) überschreiten das 5-s-Limit desevaluate_script-Runners — nur selektor-scoped Queries verwenden. Siehe auch [[sources/starlims-testsystem]].
