\# Recipe – Version 0.1



Build the first local prototype of a recipe management application.



This is an architectural prototype. The purpose of version 0.1 is to establish a clean domain model, database structure, API and basic UI that we can evaluate using real recipes.



Do not assume that the architecture or database schema must be preserved in later versions. We may deliberately discard version 0.1 and rebuild it from scratch after evaluating it.



Favor simplicity, clarity and maintainability over premature abstraction.



\## Technology



Use:



\* ASP.NET Core / C#

\* Entity Framework Core

\* PostgreSQL

\* React

\* TypeScript

\* Visual Studio as the primary development environment

\* Docker for PostgreSQL



Create a solution that can be cloned, built and run locally with minimal setup.



Use current stable/LTS versions where appropriate.



Do not introduce microservices, message queues, Kubernetes, cloud dependencies or other infrastructure that is unnecessary for this prototype.



\## Architecture



Keep the backend reasonably separated into domain/application/infrastructure/API concerns, but do not over-engineer it.



The domain model should not depend on React, HTTP, PostgreSQL or Entity Framework-specific behavior more than necessary.



Use migrations for database schema changes.



The application must be designed so that the domain model can evolve significantly during development.



\## Recipe



A Recipe should initially contain:



\* Id

\* Author

\* Name

\* Description

\* RecipeType

\* Yield

\* TotalTime

\* Tags

\* Ingredient groups

\* Ingredients

\* Cooking steps

\* Images

\* CreatedAt

\* UpdatedAt

\* Publication state



A recipe may have zero or more tags.



A recipe must have exactly one RecipeType.



\## Recipe type



RecipeType is an administrator-controlled classification.



Examples include:



\* Amuse bouche

\* Appetizer

\* Main course

\* Side dish

\* Dessert

\* Snack

\* Bread

\* Sauce

\* Drink

\* Other



Do not implement RecipeType as a C# enum.



Model it as data so that new recipe types can later be introduced without changing application code.



The model should support translated display names in the future.



\## Tags



Tags are separate entities and must not be stored as comma-separated strings.



Recipes have a many-to-many relationship with tags.



Tags should eventually be searchable and translatable.



Avoid building a complicated tag-management system in version 0.1.



\## Ingredients



Ingredients are a central, curated part of the system.



An Ingredient represents a canonical ingredient, not the text entered in a particular recipe.



For example:



Zucchini, courgette and squash may refer to the same canonical ingredient.



The model must therefore distinguish between:



\* canonical ingredient identity

\* translated ingredient names

\* aliases



Only administrators may create canonical ingredients.



Normal recipe authors must select ingredients from the existing ingredient catalogue.



Do NOT silently create new ingredients when a recipe author types an unknown ingredient.



The architecture should make it possible to later add an "ingredient request" workflow, but this workflow does not need to be implemented in 0.1.



Ingredient search should search both canonical names/translations and aliases.



Examples:



Searching for "courgette" should be able to find the canonical ingredient Zucchini.



Searching for "squash" may also find the same ingredient.



Do not make the canonical identity dependent on a particular language.



\## Recipe ingredients



A recipe ingredient is not free text.



A RecipeIngredient should reference a canonical Ingredient.



It should contain at least:



\* IngredientId

\* Amount

\* Unit

\* Preparation

\* Note

\* SortOrder



For example:



Ingredient: Parsley

Amount: 2

Unit: tablespoon

Preparation: finely chopped



Do not create separate ingredients such as "finely chopped parsley".



Ingredient groups must be supported.



Example:



\### Sauce



\* 200 ml cream

\* 1 shallot



\### Garnish



\* 50 g caviar

\* chives



\## Amounts and units



Units must be modeled as data rather than embedded strings.



The model must be capable of representing different dimensions such as:



\* mass

\* volume

\* count

\* length

\* temperature

\* package

\* other



Do not assume that the unit displayed to the recipe author is necessarily the unit displayed to every future user.



The architecture should allow future user preferences such as metric versus US customary units.



Generic unit conversions should eventually be possible, for example:



\* ml ↔ dl ↔ litre

\* ml ↔ US cup

\* g ↔ kg



Do not build a comprehensive conversion engine in version 0.1 unless it is required for the basic application to work.



Avoid storing converted values as the authoritative recipe amount.



Preserve the author's original amount and unit.



\## Ingredient-specific conversions



The architecture should allow future ingredient-specific conversions.



Example:



A recipe may specify:



165 g egg



A future presentation layer may show approximately:



3 eggs



based on an ingredient-specific average weight.



This is fundamentally different from a generic unit conversion such as dl to ml.



Do not implement a sophisticated ingredient conversion engine in 0.1, but do not design the model in a way that prevents it.



Approximate conversions should eventually be distinguishable from exact conversions.



\## Yield



Do NOT model recipe output simply as "Portions".



A recipe has a Yield consisting of an amount and a unit.



Examples:



\* 4 portions

\* 500 g

\* 1.5 litres

\* 24 pieces



This allows the same model to represent a main course, clarified butter, stock, cordial, bread, sauce, etc.



Units may indicate whether they are appropriate for ingredients, recipe yield, or both.



The model should make future recipe scaling possible.



For example:



Original yield: 500 g

Requested yield: 750 g

Scaling factor: 1.5



Do not build advanced scaling logic yet.



\## Cooking steps



Cooking instructions must consist of ordered CookingStep entities rather than one large text field.



Initially each step needs:



\* Id

\* RecipeId

\* SortOrder

\* Instruction



The architecture should make it possible to later add information such as:



\* duration

\* temperature

\* timer

\* image

\* references to ingredients



Do not implement these advanced features yet.



\## Images



A recipe may have multiple images.



Initially support:



\* Id

\* RecipeId

\* file/location reference

\* IsPrimary

\* Caption

\* SortOrder



For version 0.1 images may be stored locally on disk.



Do not store image binary data in PostgreSQL.



Design this so that local image storage can later be replaced with cloud/object storage.



\## Users and authors



Version 0.1 is a single-user local application.



However, Recipe must already reference an Author/User entity.



Create one local development user.



Do not implement registration, OAuth, external identity providers, subscriptions or payments in 0.1.



The future application is intended to support multiple users who can:



\* publish recipes

\* comment on recipes

\* rate recipes

\* contribute popular recipes



Do not implement these community features yet.



Avoid architecture that would make them unnecessarily difficult to add later.



\## Language



Internationalization is an important future requirement.



Do not hard-code Swedish names into domain entities as their permanent identity.



Ingredients, units, recipe types and eventually tags should be capable of having translated names.



The first UI may be Swedish.



Do not build a complete translation management UI in 0.1.



\## Search



The application must allow recipes to be searched by:



\* recipe name

\* one or more ingredients

\* one or more tags

\* RecipeType



These filters must be combinable.



When multiple ingredients are selected, use AND semantics by default.



Example:



Ingredients:



\* cauliflower

\* caviar



should find recipes containing both ingredients.



Ingredient search must take ingredient aliases into account.



\## UI



Create a functional but clean UI.



The main goal is usability rather than visual polish.



Implement at least:



1\. Recipe list/search

2\. Recipe detail view

3\. Create recipe

4\. Edit recipe

5\. Ingredient autocomplete/search

6\. Ingredient groups

7\. Tag selection

8\. RecipeType selection

9\. Yield entry

10\. Cooking-step editing and ordering

11\. Basic image handling



The recipe editor is particularly important.



It should be practical to enter a real, moderately complex recipe without fighting the UI.



Do not spend excessive effort on styling in this iteration.



\## Seed data



Provide development seed data containing enough data to exercise the model.



Include several:



\* RecipeTypes

\* Units

\* Tags

\* Ingredients

\* Ingredient aliases



Include examples where multiple names map to the same canonical ingredient.



Create a few example recipes demonstrating:



\* ingredient groups

\* multiple tags

\* different yield types

\* multiple cooking steps



Seed data is development/test data and should be easy to replace.



\## Tests



Add useful automated tests for important domain and application behavior.



Prioritize tests around:



\* recipe creation

\* ingredient relationships

\* ingredient aliases

\* recipe search/filtering

\* multiple ingredient AND filtering

\* tags

\* RecipeType

\* yield



Avoid tests that merely reproduce framework behavior.



\## Documentation



Create a README containing:



\* architecture overview

\* project structure

\* prerequisites

\* how to start PostgreSQL

\* how to apply migrations

\* how to run the backend

\* how to run the frontend

\* how to run tests



Also create:



`docs/DOMAIN\\\_MODEL.md`



Document the important modeling decisions and, importantly, the assumptions that are intentionally left open for future iterations.



\## Important development principles



Do not try to implement every future requirement mentioned above.



Distinguish between:



1\. functionality required for version 0.1

2\. architectural considerations that should remain possible later



Avoid speculative abstractions unless they solve a concrete problem in 0.1.



Prefer straightforward code that an experienced C# developer can inspect and understand.



Do not hide important domain behavior behind unnecessary generic frameworks.



Do not add dependencies merely because they are fashionable.



Before implementing, inspect the existing repository.



If this is an empty repository, propose a concise implementation plan and then build the solution.



If an existing implementation is present, do not assume it should be preserved. Evaluate whether it provides useful foundations for this specification.



Build the complete solution and run the relevant tests before considering the task complete.



If something is ambiguous, make the simplest reasonable decision and document the assumption in `docs/DOMAIN\\\_MODEL.md` rather than inventing large amounts of functionality.



At completion, provide a concise summary of:



\* what was implemented

\* important architectural decisions

\* assumptions made

\* tests performed

\* known limitations

\* questions or decisions that should be revisited for version 0.2

