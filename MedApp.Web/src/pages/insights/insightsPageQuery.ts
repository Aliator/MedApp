import {insightsQuery} from '../../api/insights';
import {DEFAULT_INSIGHTS_WEEKS, type InsightsSearch} from '../listSearch';

/**
 * Insights' data for its filters: what the page reads and its route's loader preloads. It's in a module of its own,
 * not the page's, so the loader (in the app's main bundle) doesn't pull in the page, and with it the chart library:
 * that loads with the page, for admins who open it.
 */
export const insightsPageQuery = (search: InsightsSearch) =>
  insightsQuery({weeks: search.weeks ?? DEFAULT_INSIGHTS_WEEKS, templateId: search.template, specialtyId: search.specialty});
