// The time zone of the browser for the pages that show times in it: the offset from UTC in minutes, east positive.
export function offsetMinutes() {
    return -new Date().getTimezoneOffset();
}
