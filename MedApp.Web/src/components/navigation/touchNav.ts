import { useMediaQuery } from "@astryxdesign/core/hooks";
import { createContext, useContext } from "react";

export const TOUCH_NAV_QUERY = "(max-width: 767px), (hover: none)";

export const useIsTouchNav = () => useMediaQuery(TOUCH_NAV_QUERY, false);

export const TouchNavContext = createContext(false);

export const useTouchNav = () => useContext(TouchNavContext);
