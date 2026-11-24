import { useMediaQuery } from "@astryxdesign/core/hooks";
import {useNavSettings} from "../../app/navSettings";
import { createContext, useContext } from "react";

export const TOUCH_NAV_QUERY = "(max-width: 767px), (hover: none)";

export function useIsTouchNav() {
  const isTouch = useMediaQuery(TOUCH_NAV_QUERY, false);
  const {alwaysShowRowMenus} = useNavSettings();
  return isTouch && !alwaysShowRowMenus;
}

export const TouchNavContext = createContext(false);

export const useTouchNav = () => useContext(TouchNavContext);
