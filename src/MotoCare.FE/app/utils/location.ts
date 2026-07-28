import type { AddressDetails, LocationOption } from '~/types/api'

export const localizeCountries = (countries: LocationOption[]): LocationOption[] => {
  const displayNames = new Intl.DisplayNames(['vi'], { type: 'region' })
  return countries
    .map(country => ({
      ...country,
      name: displayNames.of(country.code.toUpperCase()) || country.name
    }))
    .sort((left, right) => left.name.localeCompare(right.name, 'vi'))
}

export const emptyAddressDetails = (legacyAddress = ''): AddressDetails => ({
  addressLine: legacyAddress,
  countryCode: 'VN',
  countryName: 'Việt Nam',
  regionCode: '',
  regionName: '',
  areaCode: '',
  areaName: ''
})

export const normalizeAddressDetails = (
  details?: Partial<AddressDetails> | null,
  legacyAddress = ''
): AddressDetails => details
  ? {
      addressLine: details.addressLine || '',
      countryCode: details.countryCode || 'VN',
      countryName: details.countryName || (details.countryCode === 'VN' ? 'Việt Nam' : ''),
      regionCode: details.regionCode || '',
      regionName: details.regionName || '',
      areaCode: details.areaCode || '',
      areaName: details.areaName || ''
    }
  : emptyAddressDetails(legacyAddress)

export const formatAddressDetails = (details: AddressDetails) =>
  [
    details.addressLine,
    details.areaName,
    details.regionName,
    details.countryName
  ].map(value => value.trim()).filter(Boolean).join(', ')
