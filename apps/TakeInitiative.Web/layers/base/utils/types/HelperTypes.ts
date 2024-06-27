import * as yupPkg from "yup";
export const yup = yupPkg;

export type TakeInitColour =
    | "take-navy"
    | "take-navy-light"
    | "take-navy-medium"
    | "take-navy-dark"
    | "take-red"
    | "take-yellow-light"
    | "take-yellow"
    | "take-yellow-dark"
    | "take-grey"
    | "take-grey-light"
    | "take-grey-dark"
    | "white"
    | "take-teal"
    | "take-blue"
    | "take-purple"
    | "take-purple-dark"
    | "take-purple-very-dark"
    | "take-purple-light"
    | "take-red"
    | "take-yellow"
    | "take-yellow-dark"
    | "take-yellow-light"
    | "take-grey"
    | "take-grey-light"
    | "take-grey-dark"
    | "take-creme";

export const TakeInitContrastColour: Record<TakeInitColour, string> = {
    "take-navy": "white",
    "take-navy-light": "white",
    "take-navy-medium": "white",
    "take-navy-dark": "white",
    "take-red": "white",
    "take-yellow-light": "take-navy",
    "take-yellow": "take-navy",
    "take-yellow-dark": "take-navy",
    "take-grey": "white",
    "take-grey-light": "white",
    "take-grey-dark": "white",
    white: "take-navy",
    "take-teal": "take-navy",
    "take-blue": "take-grey-light",
    "take-purple": "take-grey-light",
    "take-purple-dark": "take-grey-light",
    "take-purple-very-dark": "take-grey-light",
    "take-purple-light": "take-grey-light",
    "take-creme": "take-navy",
};

export type FontAwesomeIconSize =
    | "2xs"
    | "xs"
    | "sm"
    | "lg"
    | "xl"
    | "2xl"
    | "1x"
    | "2x"
    | "3x"
    | "4x"
    | "5x"
    | "6x"
    | "7x"
    | "8x"
    | "9x"
    | "10x";
