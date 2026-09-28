# Über Bagarre — « Hyland cogne » : l'histoire complète

Un polar de quartier, entre *Rocky* et *GTA*. Des gens paumés, drôles et attachants, dans une ville
qui a l'air tranquille au soleil et qui pourrit la nuit. Chaque bagarre a une raison, et chaque coup
donné a des conséquences.

Ce document décrit l'histoire **telle qu'elle est jouée** dans le monde ouvert : qui on rencontre,
ce qui déclenche chaque étape, les choix et ce qu'ils changent, les histoires secondaires, les fins.
Le code est dans `Assets/UberBagarre/Scripts/World/OpenWorldStory.cs` (le moteur) et
`Assets/UberBagarre/Scripts/World/Saga/` (un fichier par acte, la ville, le journal).

---

## Comment l'histoire arrive

**Pas de chapitres à l'écran.** Pas de « CHAPITRE 2 » en grand, pas de menu de chapitres : le jeu
avance, c'est tout. L'histoire arrive :

- **au téléphone** : des **appels** (le téléphone sonne, on le sort pour répondre ; sans réponse au
  bout de 30 s, c'est un appel manqué et ça rappelle un peu plus tard), des **SMS** (un bandeau
  descend en haut de l'écran ; les conversations se gardent dans Messages, un contact par
  personne : Sami, maman, Nestor, Ray, Lina, Jeff, Karim, Rosa, Duval, M. Chen, Inès, Hyland Info…),
  et **l'appli Über Bagarre** qui propose les courses de l'histoire comme les autres ;
- **par les gens** : ils sont quelque part dans la ville (la salle de Ray, la laverie, le cabinet
  médical, le skatepark…) et on va leur **parler (E)** ;
- **à l'ordinateur** : les **mails**, et le journal en ligne **Hyland Info** (ses articles parlent
  de ce que tu as fait ; une alerte SMS « HYLAND INFO » prévient quand il en paraît un) ;
- **au courrier**, sur le bureau du motel (le premier matin).

En haut à gauche, une ligne d'**objectif** dit ce que l'histoire attend (et un repère GPS montre où
aller). Entre deux étapes, la ville est libre : les courses ordinaires tombent, on s'entraîne, on
mange, on joue, on vole des voitures, on fuit la police.

**La sauvegarde** se fait en dormant (le lit). Chaque étape de l'histoire est gardée par un drapeau :
une partie rechargée reprend exactement là où en était la dernière nuit. Rien ne dépend d'un
minuteur : une étape avance sur un fait (une course gagnée, une nuit passée, une porte franchie,
quelqu'un à qui on a parlé, un mail lu).

**L'heure et les jours comptent** : la ville a son horloge (une journée en 24 minutes). Jour 1 =
lundi. Certaines étapes attendent le soir ou la nuit ; le lit fait alors une **sieste** jusqu'à
l'heure voulue au lieu de faire passer la nuit.

---

## Le héros : Léo Marchal, 27 ans

- Il a grandi dans les Slums, élevé par sa mère **Martine**. Son père est parti quand il avait 6 ans.
- **Coach Ray** l'a sorti de la rue à 12 ans, à la salle de boxe du centre social.
- **Il y a 3 ans** : finale régionale à l'ancienne salle des fêtes. Au 3e round, il s'écroule, la vue
  trouble, les jambes en coton. On trouve 80 000 € de paris contre lui et la fédération le radie à
  vie. Sa mère perd son travail à cause de la honte.
- **Aujourd'hui** : motel Hyland, chambre 3, trois loyers de retard. Il doit **12 000 € à Nestor**,
  empruntés pour les frais d'hôpital de sa mère. Sa ceinture de champion amateur a disparu.
- **Ce qu'il veut** : payer sa dette, puis comprendre ce qui s'est passé ce soir-là.

## Les personnages, et où les trouver

| Personnage | Qui | Où, dans le jeu |
|---|---|---|
| **Sami Benali** | Ami d'enfance, développeur. C'est lui qui a drogué Léo il y a 3 ans (pour l'opération de sa sœur Inès), puis a créé l'appli pour « rendre » à Léo… et on la lui a prise. | Au téléphone ; au Bud's Bar à l'acte 4 |
| **Coach Ray Dumont** | 64 ans, ancien boxeur, bourru, cœur immense. Sa salle doit être rasée pour un parking. | La salle (Boxe Club, centre social), de 6 h à 22 h |
| **Lina Moreau** | Infirmière, cash et courageuse. Son frère Théo a été tabassé par un contrat Über Bagarre. | Le cabinet médical, de 8 h à 20 h |
| **Maman (Martine)** | Travaille à la laverie. Ne sait rien de ce que fait Léo. | La laverie, de 8 h à 19 h |
| **Jeff** | 16 ans, skateur du Shred Shack, voit tout, vend ses infos 20 €. | Le skatepark, de 10 h à 22 h |
| **Karim** | Livreur de Taco Ticklers (si tu l'as épargné). | Derrière le Taco Ticklers, de 11 h à 23 h |
| **M. Chen** | Patron du Dragon d'Or, racketté par les Kovac. | Le Dragon d'Or, de 11 h à 23 h |
| **Tony** | Le barbier. | Chez Tony, barbier, de 9 h à 19 h |
| **Nestor** | Usurier des Slums, gros, poli, terrifiant. | Devant le motel (le premier soir) ; au téléphone |
| **Dragan et Milan Kovac** | Les docks. Dragan calme, Milan violent et rancunier (le rival). | Courses et embuscades |
| **Rosa Delmas** | Patronne du Vertigo et de la Fosse. Élégante, froide, un code d'honneur. | La salle du fond du Vertigo, la nuit |
| **Le Taureau (Bastien Roux)** | Champion de la Fosse. Lui aussi a été payé pour perdre. | La Fosse ; la salle du fond après sa défaite |
| **Victor Sarkis, « le Comptable »** | Directeur du casino, blanchit l'argent de la ville, tient un carnet noir. | Le Casino Royal |
| **Commissaire Brandt** | Chef de la police, ex-boxeur, corrompu, bras armé du maire. | Le commissariat ; la mairie |
| **Arthur Holt, le maire** | Souriant, réélu depuis 12 ans. Le vrai propriétaire d'Über Bagarre. | La mairie, la nuit de l'élection |
| **Inspectrice Camille Duval** | Mutée de la capitale, enquête sur les agressions « commandées ». | Devant le motel ; le commissariat |
| **Mme Hélène Keller** | Candidate contre Holt, fait campagne à pied. | Le parvis de la mairie ; la poste |
| **Inès** | La petite sœur de Sami, malade du cœur. | Devant le centre médical |

---

## PROLOGUE — « Chambre 3 » (jours 1 et 2)

| # | Étape | Ce qui la déclenche | Ce qui se passe |
|---|---|---|---|
| 1 | Réveil | Nouvelle partie (14 h) | « Trois loyers de retard. Et le chauffage qui fait semblant. » |
| 2 | Le courrier | E sur le courrier du bureau | Les lettres s'ouvrent à l'écran : le **loyer**, la **lettre de Nestor**, la **carte postale de maman**. |
| 3 | L'appel de Sami | Juste après | Le téléphone sonne. Sami : l'appli. *« T'inquiète frère, c'est juste une appli. Tu cognes, tu encaisses, t'es payé. »* |
| 4 | L'appli | Le joueur ouvre le lien dans Messages | Installation, puis on lance l'appli soi-même. |
| 5 | Bruno Moretti | La nuit (après 21 h 45 ; sieste possible) | Une pluie fine, les néons du Vertigo dans les flaques. SMS de Sami. Première course, une étoile : Moretti fume devant le club. Article « Nuit agitée devant le Vertigo ». Les courses ordinaires commencent. |
| 6 | Coach Ray | Le lendemain (après une nuit) | Ray appelle. À la salle : *« Je t'ai vu à la télé. T'as l'air d'un chien battu. »* Les bases : **la garde** tenue 3 s, **trois esquives**, puis un **sparring** contre Petit Louis. *« J'ai jamais cru que t'avais triché. »* |
| 7 | Nestor | Le soir (après 19 h), en rentrant au motel | Nestor et deux gros bras devant le motel : **7 jours pour 1 500 €**, puis 1 500 € chaque semaine. Virement par l'appli Banque. |

## ACTE 1 — « Les petites notes » (jours 3 à 9)

| # | Étape | Déclencheur | Ce qui se passe |
|---|---|---|---|
| 1 | Les courses | SMS de Sami | Trois courses et **deux étoiles** proposées (réputation et niveau). L'objectif compte. |
| 2 | **Karim le livreur** (premier choix) | Course de l'appli, derrière le Taco Ticklers | Il lève les mains : **le frapper** (300 €, Karim à l'hôpital, Code −8, article) ou **le laisser parler** (il a refusé de livrer de la drogue pour les Kovac ; allié : livraisons gratuites, infos, planque ; Code +10). |
| 3 | Jeff | SMS d'un inconnu | Au skatepark : Jeff voit tout. Dragan fait ses affaires derrière la pizzeria. Vingt balles l'info. |
| 4 | Dragan Kovac | Le soir (après 18 h) | Course deux étoiles, consigne : lui casser le nez. Article « Règlement de comptes derrière la pizzeria ». L'arcade ouverte. |
| 5 | Lina | Juste après | Au cabinet médical : *« T'es le boxeur radié. »* Théo, son frère. (Si Karim a été frappé, elle en parle.) Elle soigne tout. |
| 6 | **Milan se venge** | La nuit, en rentrant au motel | Embuscade : **Milan et trois gars, 4 contre 1**. Article « Bagarre générale au motel ». Milan devient rancunier. Il laisse tomber son téléphone. |
| 7 | **Duval** | Le lendemain matin | On frappe. *« Une appli qui s'appelle… Über Bagarre. Ça vous dit quelque chose ? »* Trois réponses (nier, plaisanter, promettre de l'appeler) : sa confiance en dépend. Elle sait si tu as payé des flics. Sa carte, son numéro. |
| 8 | Fin d'acte | Un peu plus tard | Dans le téléphone de Milan : **6 cibles sur 8 devaient de l'argent au casino**. |

## ACTE 2 — « La Fosse » (jours 10 à 16)

| # | Étape | Déclencheur | Ce qui se passe |
|---|---|---|---|
| 1 | L'invitation | Appel de Sami | La Fosse, derrière la porte du fond du Vertigo. |
| 2 | Le test | La nuit, dans la salle du fond | Rosa : pas d'appli, pas de photo, pas de triche. Combat de test contre la Recrue. |
| 3 | **La Ligue** | Un combat par soir (la nuit, salle du fond) | **Le Facteur** (10e, frappe vite), **Mama Olga** (7e, ancienne lutteuse, très coriace), **le Dentiste** (4e, poing américain : il frappe très fort), **Sven** (2e, colosse lent et lourd). Chacun son style de combat. |
| 4 | **La ceinture** | Après le premier combat de Ligue (SMS de Jeff) | Chez le prêteur sur gages : ta ceinture en vitrine. Vendue il y a 3 ans par un certain **« S. B. »**. La racheter (2 000 €) maintenant ou plus tard. |
| 5 | **La salle de Ray menacée** | Après le deuxième combat | Article : le maire annonce un parking à la place du centre social. Ray appelle : un **tournoi** pour payer un avocat (histoire secondaire). |
| 6 | **Le Taureau** | Le vendredi soir (ou le lendemain de la Ligue si vendredi est trop loin) | Le titre. Au sol : *« Il y a 2 ans, un type du casino m'a payé pour tomber. Il m'a dit : comme le petit Marchal. »* Champion de la Fosse. |
| 7 | Les archives | Juste après | À l'ordinateur, Hyland Info : l'article d'il y a 3 ans. L'argent des paris est passé par **SARK Holding**, domiciliée au casino. |
| 8 | Fin d'acte | Appel de Rosa | *« Sarkis veut te voir. Personne ne dit non à Sarkis. »* |

## ACTE 3 — « Le Comptable » (jours 17 à 23)

| # | Étape | Déclencheur | Ce qui se passe |
|---|---|---|---|
| 1 | Le rendez-vous | Le soir, au casino | Sarkis offre **50 000 €** pour perdre le prochain combat, comme il y a trois ans. **Faire semblant d'accepter** (acompte de 5 000 €, Code −3) ou **refuser** (Code +3). |
| 2 | **Le plan** | Juste après | Voler le carnet noir. Trois personnes, dans l'ordre qu'on veut : **Tony** (uniforme de serveur), **Jeff** (les rondes : une minute de pause café toutes les dix minutes), **Karim** (la porte des livraisons, code 1-9-7-4). Si Karim a été frappé : il est au centre médical, plâtré — le payer, le menacer, ou lui demander pardon. |
| 3 | **La nuit du casse** | Après 23 h, derrière le casino | **En force** (trois vigiles, puis le bureau ; Code −3), **en douceur** (le SMS de Jeff : une minute pour atteindre le bureau ; réussi, Code +2 ; raté, un vigile), ou **les deux** (en douceur, et ça dérape à la fin). |
| 4 | Sarkis | Dans son bureau | Le combat (avec un garde du corps, sauf en douceur). Article « Casino Royal : nuit agitée ». |
| 5 | **Le carnet** — le gros choix | Le coffre | Les noms, les montants… et **« H. »** partout. Au sol, Sarkis crache le nom : **Sami Benali**, vingt mille euros. Puis : **Brandt**, **Duval**, ou **le garder**. |
| 6 | Ce que devient le carnet | Juste après | **Brandt** : un piège — garde à vue, tabassé, 30 % de l'argent « perdu » ; Duval te sort de là, furieuse (confiance −1). **Duval** : une vraie enquête ; la police corrompue devient ton ennemie (plus de pots-de-vin possibles) ; Code +15. **Le garder** : chantage, 15 000 € de virements anonymes, Code −20 ; Lina et Ray s'éloignent. |

## ACTE 4 — « Le nom » (jours 24 à 28)

| # | Étape | Déclencheur | Ce qui se passe |
|---|---|---|---|
| 1 | Le doute | SMS de Sami, le soir au Bud's Bar | Sami fait comme si de rien n'était. Il ment mal. Il file. |
| 2 | **La filature** | Juste après | Suivre Sami à pied ou en voiture jusqu'aux docks : **entre 8 et 50 m**. Trop loin plus de 10 s : perdu. Trop près plus de 2,5 s : il se retourne. Raté, il repasse au Bud's Bar le lendemain soir. |
| 3 | La vérité | Aux docks | *« Inès allait mourir, Léo. Ils m'ont donné 20 000 balles pour mettre un truc dans ta bouteille. J'ai créé l'appli pour te rendre ton argent… et ils me l'ont prise. »* Il a aussi vendu la ceinture. |
| 4 | **Le choix** | Juste après | **Pardonner** (allié pour la fin, accès aux serveurs ; Code +15). **Frapper** (un combat ; puis Inès t'écrit, devant le centre médical : scène dure ; Code −10). **Le livrer à Rosa** (il disparaît ; article « Disparition d'un jeune développeur » ; Code −25). |
| 5 | **La vengeance de Milan** | Si Milan est rancunier (il l'est) | SMS depuis le téléphone de Jeff : Milan l'a enlevé. **L'usine du chantier**, Milan et trois gars, plus forts qu'au motel. Jeff a entendu « oui, monsieur le maire ». |
| 6 | Fin d'acte | Appel de Sami (s'il est pardonné) ou SMS de Jeff | Un mail : dans les serveurs de l'appli, le compte administrateur est **a.holt@mairie-hyland.fr**. Prochaine cible commandée : Mme Keller. |

## ACTE 5 — « Le roi d'Hyland » (la dernière semaine)

| # | Étape | Déclencheur | Ce qui se passe |
|---|---|---|---|
| 1 | La campagne | Appel de Duval (si elle te fait confiance) ou de Jeff | Mme Keller, cinq étoiles sur sa tête. **Deux escortes** à pied : la mairie → la poste, puis le lendemain la poste → la mairie. Elle t'attend si tu t'éloignes de plus de 30 m ; à mi-chemin, des casseurs cagoulés (un de plus la deuxième fois). |
| 2 | **La chasse à l'homme** | Après la seconde escorte | Brandt fait de toi un « individu dangereux » : **recherché en permanence pendant deux jours** (au moins deux étoiles, trois si Duval ne te couvre pas ; perdre leur trace les envoie chercher ailleurs, pas plus). |
| 3 | **Les alliés** | Pendant la chasse | **Karim** : planque derrière le Taco Ticklers. **M. Chen** : sa cave. Dans une planque, la police perd ta trace trois fois plus vite. **Duval** (si elle te fait confiance) : une fois par jour, à trois étoiles, elle appelle le central et on te lâche un moment. **Rosa et le Taureau** : ils promettent d'être à la mairie. |
| 4 | **La nuit de l'élection** | La nuit, à la mairie | Si tu as battu le Taureau, lui et Rosa se battent à tes côtés : deux flics de Brandt à passer au lieu de quatre. Puis **Brandt** dans le hall : un vrai boss, il connaît la boxe. |
| 5 | **Holt** | Son bureau | Il ne se bat pas, il négocie : *« Prends l'appli. Prends la ville. Tu seras moi, en plus jeune. »* **Accepter**, **refuser**, ou **prendre l'argent du coffre et partir**. |

---

## Les fins

| Fin | Condition | Ce qui se passe |
|---|---|---|
| **Le Justicier** | Refuser Holt **avec un Code ≥ 20 et le carnet donné à Duval** | Duval arrête Holt ; Brandt tombe ; Keller est élue ; **l'appli est fermée** (plus de courses) ; ta radiation est annulée. Dernier combat : un **vrai match officiel** à la salle de Ray, contre le champion régional Éric Vance — Ray dans ton coin, ta mère et Lina dans la salle. |
| **Le Roi** | Accepter l'offre de Holt | Tu reprends l'appli, 50 000 €, tu vis au **manoir**. Ray ne te parle plus, Lina est partie. Dernière image : toi, seul sur la terrasse. |
| **Le Fantôme** | Prendre l'argent et partir — ou refuser Holt sans pouvoir le faire tomber | 30 000 € du coffre ; toi et ta mère quittez Hyland au lever du soleil. |
| **La Chute** | Pendant l'acte 5 : **trois arrestations**, ou **trop de blessures** (trois K.O., ou deux morts) | Prison à vie, ou funérailles sous la pluie. Retour à l'écran titre : « Continuer » reprend à la dernière nuit. |
| **Fin secrète** | Le Justicier **et** : Nestor remboursé, M. Chen aidé, le tournoi de Ray gagné, la ceinture rachetée, au moins trois visites à maman, champion de la Fosse, une note moyenne d'au moins 4,5 étoiles, et Sami pas livré à Rosa | Après le match officiel, Sami réapparaît au fond de la salle, avec **Inès, guérie**. |

Après une fin (sauf la Chute), la partie est sauvegardée et la ville continue.

---

## Le Code (Brute ↔ Justicier)

Une jauge de −100 (la Brute) à +100 (le Justicier). Chaque changement s'affiche un instant
(« ▲ JUSTICIER — … » / « ▼ BRUTE — … »). Elle change **les dialogues** (maman, Lina, Ray), **les
alliés** (Duval ne te fait confiance qu'avec un Code d'au moins 10 et un casier léger, ou le carnet)
et **la fin** (le Justicier demande 20).

| + | − |
|---|---|
| Écouter Karim (+10), promettre à Duval de l'appeler (+2), refuser Sarkis (+3), le casse en douceur (+2), le carnet à Duval (+15), demander pardon à Karim (+4), pardonner à Sami (+15), voir Inès (+3), aider M. Chen (+8), le tournoi de Ray (+3 puis +5), racheter la ceinture (+2), laisser de l'argent à maman (+2) | Frapper Karim (−8), accepter l'argent de Sarkis (−3), le casse en force (−3), menacer Karim (−5), le chantage (−20), frapper Sami (−10), livrer Sami (−25) ; et chaque délit vu par la police (−1), intimider un témoin (−1), s'acharner sur quelqu'un au sol (−2), payer un flic (−6) |

---

## Les histoires secondaires

- **Nestor.** 1 500 € par semaine, par l'appli Banque (« Rembourser Nestor »). Un SMS deux jours
  avant l'échéance (« tu en es à … sur … »). En retard : **300 € d'intérêts**, trois jours de
  délai, et **ses encaisseurs** t'attendent le soir devant le motel — un de plus et plus costauds à
  chaque visite (rivaux qui reviennent). Perdu contre eux : ils se servent (jusqu'à 1 000 €).
  Dette soldée : *« On est quittes, Léo. »*
- **Maman, à la laverie.** Une vraie visite par semaine (après, elle te renvoie gentiment). Ce
  qu'elle dit dépend de ce qu'elle a appris : la bagarre du motel dans le journal, une nuit au
  poste, la chasse à l'homme à la télé, ton Code (« tu as le regard de ton père, quand il rentrait
  tard »), la dette. On peut lui laisser 200 €. Des SMS un jour sur deux, la chapelle le dimanche.
- **M. Chen.** Après Dragan, Karim (ou Jeff) t'en parle. Au Dragon d'Or : les Kovac viennent
  chaque jeudi chercher « l'enveloppe ». Accepter → les racketteurs le soir (après 19 h) devant le
  restaurant. Ensuite : un repas gratuit par jour, trois repas dans ton frigo, sa cave comme planque
  à l'acte 5.
- **Karim** (épargné) : deux tacos dans ton frigo un jour sur deux, un repas quand tu passes.
- **Le tournoi de Ray** (acte 2, et on peut changer d'avis en retournant le voir) : trois combats
  propres à la salle, un par soir (après 19 h) — Djibril, le Boucher, Gros Marco. Perdu, on refait
  le combat le lendemain. Gagné : de quoi payer un avocat contre le parking.
- **La ceinture** : tant qu'elle n'est pas rachetée, le vendeur la propose à chaque passage.
- **Le Dentiste** : viré de la Fosse pour triche, il t'attend une nuit devant le motel (à partir de
  l'acte 3) avec son « assistant ».
- **Lina** : un SMS tous les trois jours (Théo, les petites victoires… ou la distance si tu
  deviens la Brute). Au cabinet, elle recoud gratuitement.
- **Jeff** : 20 € l'info — une astuce sur ce qui t'attend.
- **Duval** voit tout : payer un flic de Brandt lui fait perdre confiance.

## Le rythme de la semaine

- **Vendredi** : la Fosse, les gros paris (SMS de Rosa), le titre.
- **Samedi** : soirée casino (SMS de Jeff).
- **Dimanche** : maman va à la chapelle.
- **Mercredi** : M. Chen prévient que « demain, c'est jeudi ».
- Le loyer du motel (450 €) tous les sept jours ; les échéances de Nestor.

## La police dans l'histoire

- **Actes 1 à 3** : on peut payer les flics (R devant un agent, deux étoiles au plus). Ça marche,
  mais le Code baisse et Duval l'apprend.
- **Carnet donné à Duval** : les hommes de Brandt ne prennent plus d'argent.
- **Acte 5** : la chasse à l'homme (étoiles minimum pendant deux jours), les planques des alliés,
  Duval qui efface les étoiles une fois par jour si elle te fait confiance. Trois arrestations dans
  l'acte : la Chute.

## Hyland Info

Les articles paraissent au fil de l'histoire (alerte SMS) et se lisent sur l'ordinateur : la nuit
devant le Vertigo, le livreur hospitalisé, le règlement de comptes de la pizzeria, la bagarre du
motel, l'inspectrice de la capitale, le « Parking de l'Avenir », le nouveau champion de la Fosse,
**les archives** (la finale d'il y a 3 ans et SARK Holding), la nuit agitée du casino, l'enquête
(ou le suspect « tombé dans l'escalier », ou le chantage), la disparition de Sami, l'usine du
chantier, l'élection, Mme Keller attaquée, l'avis de recherche, puis selon la fin : l'arrestation
de Holt, la fermeture de l'appli, la radiation annulée, le match officiel — ou le nouveau
« conseiller sécurité », ou la disparition, ou la chute. Et le tournoi de Ray, le Dragon d'Or qui
respire.

---

## Pour tester

- **Tab** (menu de test) → **HISTOIRE** : l'acte en cours, le jour, le Code, et six boutons
  (**PROLOGUE, ACTE 1 … ACTE 5**) qui démarrent une partie neuve à cet acte, comme si l'on avait joué
  jusque-là avec les choix du Justicier (Karim épargné, carnet à Duval, Sami pardonné…), l'argent,
  l'expérience, la réputation et le Code de ce moment-là.
- La scène doit être **reconstruite** (menu Uber Bagarre → 3b) pour que la distribution et les lieux
  existent. Sans eux, l'histoire se déroule quand même (les scènes de dialogue passent) mais
  personne n'attend dans la ville.

## Les fichiers

| Fichier | Rôle |
|---|---|
| `Scripts/World/OpenWorldStory.cs` | Le moteur : lancement, sauvegarde, attentes (appel, SMS, parler à quelqu'un, rejoindre un lieu, un combat, un choix, la nuit, le lendemain), le Code. |
| `Scripts/World/Saga/OpenWorldStory.Prologue.cs` … `Acte5.cs` | Le fil principal, acte par acte, et les fins. |
| `Scripts/World/Saga/OpenWorldStory.Ville.cs` | Les habitués, Nestor, maman, M. Chen, le tournoi, le Dentiste, les SMS du quotidien, les visites ; le saut à un acte (test). |
| `Scripts/World/Saga/OpenWorldStory.Journal.cs` | Les articles de Hyland Info et les mails. |
| `Scripts/Story/Saga/StoryActor.cs`, `StoryCast.cs` | Les gens à qui l'on parle, les lieux, les trajets à pied (filature, escorte). |
| `Scripts/UI/ChoicePrompt.cs`, `Scripts/UI/NewsFeed.cs` | Les choix (touches 1 à 4, flèches, souris), le journal. |
| `Editor/OpenWorldSceneBuilder.Saga.cs`, `…Histoire.cs` | La distribution, les lieux, les adversaires de l'histoire. |
