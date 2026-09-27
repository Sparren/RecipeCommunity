# Recipe 0.1 – produktbeslut och genomförande

Senast uppdaterad 2026-09-27. Detta dokument beskriver senare godkända iterationer; `SPECIFICATION.md` är den ursprungliga specifikationen. Det aktuella domänbeteendet och API-kontrakten dokumenteras i `DOMAIN_MODEL.md`.

## Genomfört: tre aktivitetslägen

Lägena är olika aktiviteter för samma användare, inte kontotyper eller nya roller.

| Läge | Implementerat nu |
| --- | --- |
| **Hitta & laga** | Standardingång. Sök och läs publicerade recept, visa bilder och skriv ut. En diskret genväg till redigering visas på egna recept. |
| **Skapa & utforska** | Mina recept, statusfilter för utkast/publicerade, skapa/redigera/publicera och skapa en saknad ingrediens utan att börja på ett recept. Inline-skapandet i recepteditorn finns kvar. |
| **Administration** | Samlad ingång till ingredienskatalog, taggar och recepttyper. Visas endast för administratörer; servern upprätthåller behörigheterna. |

Hitta & laga visar inte utkast. Mina recept hämtas med serverns aktuella användar-ID och innehåller den användarens egna utkast och publicerade recept. Utkast kan endast läsas av ägaren, även via direktlänk och bild-API. En gammal direktlänk till ett eget utkast öppnas i skaparläget. Egna publicerade recept kan också läsas i Hitta & laga.

Receptsökningens tidigare semantik kvarstår: ingredienser AND, taggar OR, kategorier AND. Alias, stabila katalog-ID:n, aktiva/inaktiva taggar och recepttyper, Ger, mängdintervall, bildhantering, utskriftens sidbrytningsregler och varning vid osparade receptändringar är bevarade.

## Genomfört: publicering kräver aktiva ingredienser

En vanlig användares nya ingrediens blir PendingReview och är tillgänglig för skaparen i egna utkast. En administratörs nya ingrediens blir Active. Fristående skapande och inline-skapande använder samma katalogtjänst och skapar samma slags kanoniska ingrediens.

Recept med PendingReview-ingredienser får sparas och ändras som utkast men får inte publiceras. Regeln gäller både nya recept, publicering av ett utkast och ändringar av redan publicerade recept. Servern kontrollerar databasens aktuella status och avslår innan receptändringar sparas. Felmeddelandet namnger berörda ingredienser på svenska. Editorn visar samma hinder, markerar väntande ingredienser och erbjuder **Spara som utkast** även när publicering är vald. Efter godkännande eller byte till aktiva ingredienser gäller de vanliga fullständighetskraven för publicering.

Ingen ny databasmodell, migration eller ändring av befintliga recept behövs för denna iteration. Tidigare iterationers migrationer kvarstår. Testanvändare och exempeldata för verifieringen skapas i isolerade testdatabaser.

## Beslutad framtida riktning – inte implementerad

| Område | Framtida riktning |
| --- | --- |
| Favoriter, betyg och kommentarer | Hör hemma i Hitta & laga. Inga sådana funktioner, tabeller eller platshållarknappar finns nu. |
| Foodpairing | Ska kunna stödja både att hitta mat att laga i Hitta & laga och att utforska kombinationer i Skapa & utforska. Utgår från kanoniska ingrediens-ID:n; relationer, datakällor och algoritmer är ännu inte definierade. |
| Granskning och sammanslagning | Framtida godkännande av PendingReview eller sammanslagning med en befintlig ingrediens. Ingen ny modereringsmotor eller mergefunktion är byggd. |
| Språk | Separata preferenser för UI-språk, prioriterade receptspråk och sökspråk. Receptöversättningar och användarinställningar är fortsatt framtida arbete. |
| Identitet och distribution | Autentisering, fullständig fleranvändarsäkerhet och verklig offentlig distribution återstår. |

**Publicerad** är fortfarande en lokal prototypstatus. Appen använder fortfarande den seedade lokala administratören och ska inte exponeras på internet. Vanliga användares beteende verifieras med serverbaserade testidentiteter, inte med en användarväxlare eller låtsasinloggning i produkten.

## Nästa manuella granskning

- Är skillnaden mellan Hitta & laga och Mina recept tydlig utan att skapa onödiga omvägar?
- Är övergången från läsning av ett eget recept till redigering och tillbaka begriplig?
- Är den fristående ingrediensingången lätt att hitta och återanvända i ett recept?
- Förklarar publiceringshindret vilka ingredienser som berörs och hur arbetet kan sparas vidare?
- Fungerar navigation, sökning och utskrift lika bra på smal skärm och med riktiga recept?
