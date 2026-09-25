export type Quantity = { value: number | null; maximumValue: number | null };
export type Yield = { quantity: Quantity; unitId: string };
export type Lookup = { id: string; name: string };
export type Unit = Lookup & {
  symbol: string;
  dimension: string;
  forIngredients: boolean;
  forYield: boolean;
};
export type Catalogue = {
  recipeTypes: Lookup[];
  tags: Lookup[];
  units: Unit[];
};
export type Ingredient = Lookup & {
  names: { language: string; name: string }[];
  aliases: { language: string | null; name: string }[];
};
export type Entry = {
  id?: string;
  clientKey?: string;
  ingredientId: string;
  ingredientName: string;
  quantity: Quantity;
  unitId: string | null;
  amountDescription: string | null;
  preparation: string | null;
  note: string | null;
};
export type Group = {
  id?: string;
  clientKey?: string;
  heading: string | null;
  ingredients: Entry[];
};
export type Step = { id?: string; clientKey?: string; instruction: string };
export type RecipeForm = {
  name: string;
  description: string | null;
  recipeTypeId: string;
  yield: Yield;
  totalTimeMinutes: number | null;
  publicationState: "Draft" | "Published";
  tagIds: string[];
  ingredientGroups: Group[];
  cookingSteps: Step[];
};
export type RecipeImage = {
  id: string;
  url: string;
  caption: string | null;
  isPrimary: boolean;
  sortOrder: number;
};
export type RecipeDetail = RecipeForm & {
  id: string;
  authorName: string;
  createdAt: string;
  updatedAt: string;
  images: RecipeImage[];
};
export type RecipeSummary = Omit<
  RecipeForm,
  "ingredientGroups" | "cookingSteps"
> & { id: string; recipeTypeName: string; primaryImageUrl: string | null };
export type Page<T> = {
  items: T[];
  total: number;
  pageNumber: number;
  pageSize: number;
};
