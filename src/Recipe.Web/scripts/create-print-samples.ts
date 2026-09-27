import { createPrintSamples } from "./printSamples.ts";

// Explicit, repeatable opt-in: does not reset, reseed or overwrite existing recipes.
for (const recipe of await createPrintSamples(process.env.RECIPE_API_URL)) {
  console.log(`${recipe.name}: http://127.0.0.1:5173/#/recipes/${recipe.id}`);
}
